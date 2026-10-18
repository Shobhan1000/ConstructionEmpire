using System;
using System.IO;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Management;
namespace ConstructionEmpire.Systems.Persistence
{
    /// <summary>Incremental reconstruction. The live bus/session is untouched until Commit.</summary>
    public sealed class SessionReplay : IDisposable
    {
        private readonly SessionSaveData save;
        private readonly GameEvents.Domain domain=new GameEvents.Domain();
        private int index;private bool committed,disposed;
        public ManagementSession Session {get;private set;}
        public bool Complete {get;private set;}
        public double Progress=>save.Clock.TotalHours==save.Initial.Clock.TotalHours?(Complete?1:0):(double)(Session.Simulation.Time.TotalHours-save.Initial.Clock.TotalHours)/(save.Clock.TotalHours-save.Initial.Clock.TotalHours);
        public SessionReplay(SessionSaveData source)
        {
            SessionSaveService.Validate(source);save=SessionJson.Copy(source);
            using(domain.Activate())Session=SessionFactory.Create(save.Initial);
        }
        public bool Step(int budget=256)
        {
            if(disposed)throw new ObjectDisposedException(nameof(SessionReplay));if(budget<1)throw new ArgumentOutOfRangeException(nameof(budget));if(Complete)return true;
            using(domain.Activate())
            {
                var clock=Session.Simulation.Time;clock.Speed=SimulationSpeed.Normal;
                while(budget-->0)
                {
                    if(index<save.Commands.Count && save.Commands[index].Hour==clock.TotalHours){Session.Commands.Replay(save.Commands[index++]);continue;}
                    long target=index<save.Commands.Count?save.Commands[index].Hour:save.Clock.TotalHours;
                    if(clock.TotalHours<target){var before=clock.TotalHours;Session.Simulation.Advance(save.Initial.Clock.SecondsPerTick);if(clock.TotalHours!=before+1)throw new InvalidDataException("Replay must advance exactly one fixed tick.");continue;}
                    if(index!=save.Commands.Count || clock.TotalHours!=save.Clock.TotalHours)throw new InvalidDataException("Invalid command timeline.");
                    clock.RestorePlaybackState(save.Clock);
                    if(SessionStateProof.Compute(Session)!=save.StateProof)throw new InvalidDataException("Restored state does not match this save; the live game has been preserved.");
                    Complete=true;return true;
                }
            }
            return false;
        }
        /// <summary>Call only after disposing the former simulation; UI listeners stay on the live bus.</summary>
        public ManagementSession Commit(){if(!Complete || disposed || committed)throw new InvalidOperationException("Restoration is not ready to commit.");domain.Attach();committed=true;return Session;}
        public IDisposable ActivateForVerification()=>domain.Activate();
        public void Dispose(){if(disposed)return;disposed=true;if(!committed && Session!=null){using(domain.Activate()){Session.Dispose();Session.Simulation.Dispose();}}}
    }
}
