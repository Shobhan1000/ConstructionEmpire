using System;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Simulation.Events;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Randomness;

namespace ConstructionEmpire.Systems.Simulation
{
    public sealed class SimulationManager : IDisposable
    {
        private readonly List<IRegisterableSystem> systems = new List<IRegisterableSystem>();
        private bool disposed;
        private readonly Action unsubscribe;
        private readonly CrewEfficiencyConfiguration constructionSettings;
        public IReadOnlyList<ProjectProgressEngine> ProjectEngines=>systems.OfType<ProjectProgressEngine>().ToList();
        public ProjectProgressEngine CreateProjectEngine()
        {ThrowIfDisposed();var engine=new ProjectProgressEngine(constructionSettings);engine.Conditions=Construction.Conditions;Register(engine);return engine;}
        private void BindEngine(IRegisterableSystem system)
        {
            if(!(system is ProjectProgressEngine engine) || ReferenceEquals(engine,Construction))return;
            var scheduler=Construction.Scheduling;
            if(scheduler==null){if(Construction.HasProjects)throw new InvalidOperationException("Enable shared scheduling before starting the first project when using multiple engines.");scheduler=new ConstructionEmpire.Systems.Scheduling.SchedulingSystem();Construction.EnableScheduling(scheduler);}
            engine.EnableScheduling(scheduler);
        }
        public GameTimeManager Time { get; }
        public CompanyData Company { get; }
        public SeededRandom Random { get; }
        public ProjectProgressEngine Construction { get; }
        public int RegisteredSystemCount => systems.Count;

        public SimulationManager(CompanyData company, GameTimeData clock, int seed, CrewEfficiencyConfiguration constructionConfiguration = null)
        {
            Company = company ?? throw new ArgumentNullException(nameof(company));
            Company.Validate();
            Time = new GameTimeManager(clock);
            Random = new SeededRandom(seed);
            constructionSettings=constructionConfiguration ?? new CrewEfficiencyConfiguration();
            Construction = new ProjectProgressEngine(constructionSettings);
            Register(Construction);
            unsubscribe=GameEvents.SubscribeOwned<HourlyTickEvent>(DriveSystems);
        }

        public void Register(IRegisterableSystem system)
        {
            ThrowIfDisposed();
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (!systems.Contains(system)) {BindEngine(system);systems.Add(system);}
        }

        public void RegisterBefore(IRegisterableSystem system,IRegisterableSystem before)
        {ThrowIfDisposed();if(system==null)throw new ArgumentNullException(nameof(system));if(systems.Contains(system))return;int index=systems.IndexOf(before);if(index<0)throw new ArgumentException("Preceding system is not registered.");BindEngine(system);systems.Insert(index,system);}

        public bool Unregister(IRegisterableSystem system)
        {
            ThrowIfDisposed();
            return systems.Remove(system);
        }

        public int Advance(double elapsedRealSeconds)
        {
            ThrowIfDisposed();
            return Time.Advance(elapsedRealSeconds);
        }

        public SaveGameData CaptureSave()
        {
            ThrowIfDisposed();
            return new SaveGameData
            {
                Company = Company.Copy(),
                Clock = Time.CaptureState(),
                SimulationSeed = Random.Seed
            };
        }

        private void DriveSystems(HourlyTickEvent payload)
        {
            if (disposed || !ReferenceEquals(payload.Source, Time)) return;
            TickContext context = payload.Context;
            // Registration edits made during a tick take effect on the following tick.
            var snapshot = systems.ToArray();
            var engines=snapshot.OfType<ProjectProgressEngine>().ToArray();
            if(engines.Length>1 && engines.Any(e=>!ReferenceEquals(e.Scheduling,Construction.Scheduling)))throw new InvalidOperationException("All project engines must share company-wide scheduling.");
            foreach(var engine in engines)engine.ReleaseExpiredBookings(context.TotalHours);
            foreach (var system in snapshot) system.OnTick(context);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SimulationManager));
        }

        public void Dispose()
        {
            if (disposed) return;
            unsubscribe();
            systems.Clear();
            disposed = true;
        }
    }
}
