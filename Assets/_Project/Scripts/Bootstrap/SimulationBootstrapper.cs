using System.Collections;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Events;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Definitions;
using ConstructionEmpire.Systems.Persistence;
using ConstructionEmpire.Systems.Simulation;
using UnityEngine;

namespace ConstructionEmpire.Bootstrap
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SimulationBootstrapper : MonoBehaviour
    {
        [SerializeField] private SimulationConfiguration configuration;
        public ManagementConfiguration ManagementConfiguration;
        public ManagementSession Management { get; private set; }
        private static SimulationBootstrapper activeDriver;
        public SimulationManager Simulation { get; private set; }
        public GameTimeManager GameTime => Simulation?.Time;
        public CompanyData Company => Simulation?.Company;
        public bool IsLoading { get; private set; }
        public string StorageStatus { get; private set; } = "Ready";
        public Func<string> CapturePresentation;
        public Action<string> ValidatePresentation;
        public string LoadedPresentation {get;private set;}
        public string SaveDirectoryOverride {private get;set;}
        [SerializeField, Min(30)] private float autosaveIntervalSeconds = 300;
        private float autosaveElapsed;
        private long lastAutosaveHour;
        public int MostRecentSaveSlot
        {
            get { int latest=-1;DateTime timestamp=DateTime.MinValue;
                for(int slot=0;slot<=3;slot++)if(HasSave(slot)){var date=File.GetLastWriteTimeUtc(SlotPath(slot));if(date>timestamp){timestamp=date;latest=slot;}}
                return latest; }
        }
        public string SavePath => SlotPath(1);
        public string SlotPath(int slot,bool backup=false)
        { if(slot<0 || slot>3)throw new ArgumentOutOfRangeException(nameof(slot));return Path.Combine(SaveDirectoryOverride??Path.Combine(Application.persistentDataPath,"Saves"),"slot-"+slot+".cesave")+(backup?".bak":""); }
        public bool HasSave(int slot,bool backup=false)=>File.Exists(SlotPath(slot,backup));
        private void Status(string message){StorageStatus=message;GameEvents.Publish(new SessionStorageChangedEvent(this,message,IsLoading));}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => activeDriver = null;

        public void Configure(SimulationConfiguration value) => configuration = value;

        private void Awake()
        {
            if (activeDriver != null && activeDriver != this)
            {
                Debug.LogError("Only one SimulationBootstrapper may drive the simulation.", this);
                enabled = false;
                return;
            }
            if (configuration == null)
            {
                Debug.LogError("Assign a SimulationConfiguration or run Create Simulation Bootstrap.", this);
                enabled = false;
                return;
            }
            try
            {
                if(ManagementConfiguration==null)Simulation=new SimulationManager(configuration.CreateCompany(),configuration.CreateClock(),configuration.Seed,configuration.ConstructionSettings?.CrewEfficiency);
                else {Management=SessionFactory.Create(CreateOrigin());Simulation=Management.Simulation;}
                activeDriver = this;
                Debug.Log($"Simulation ready: {Company.CompanyName}, cash {Company.CashBalance}, " +
                    $"reputation {Company.Reputation}, seed {configuration.Seed}.", this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private SessionStartData CreateOrigin(ConstructionEmpire.Data.Expansion.CareerOptions career=null)
        {
            if(ManagementConfiguration==null) throw new InvalidOperationException("Management configuration is missing.");
            var config=ManagementConfiguration;
            var workforce=new StarterWorkforce();
            workforce.Workers.AddRange(config.Employees.Select(x=>x.CreateRuntimeData()));
            workforce.Equipment.AddRange(config.Equipment.Select(x=>x.CreateRuntimeData()));
            var phaseMap=new Dictionary<string,List<ConstructionPhaseRuntimeData>>();
            foreach(var asset in config.Projects)
            {
                var definitions=asset.Template!=null && asset.Template.Phases.Count>0 ? asset.Template.Phases : config.DefaultPhases;
                phaseMap.Add(asset.Data.Id,definitions.Select(x=>x.CreateRuntimeData()).ToList());
            }
            var settings=UnityEngine.JsonUtility.FromJson<ManagementSettings>(UnityEngine.JsonUtility.ToJson(config.Settings));
            if(config.Materials.Count>0) settings.Materials=config.Materials.Select(x=>x.CreateRuntimeData()).ToList();
            var expansion=SessionJson.Copy(config.Expansion);expansion.ProceduralOpportunities=config.EnableProceduralOpportunities;
            if(expansion.OpportunityProfiles==null || expansion.OpportunityProfiles.Count==0)expansion.OpportunityProfiles=ConstructionEmpire.Data.Expansion.OpportunityProfileCatalog.Create();
            var origin=new SessionStartData {
                Company=configuration.CreateCompany(),Clock=configuration.CreateClock(),Seed=configuration.Seed,CrewEfficiency=configuration.ConstructionSettings?.CrewEfficiency,
                Projects=config.Projects.Select(x=>x.CreateRuntimeData()).ToList(),Workforce=workforce,Phases=phaseMap,Rates=config.Bidding.Rates,Bidding=config.Bidding.Bidding,
                Competitors=config.Competitors.Select(x=>x.CreateRuntimeData()).ToList(),Management=settings,Finance=config.Finance,
                Suppliers=config.Suppliers.Count==0?null:config.Suppliers.Select(x=>x.CreateRuntimeData()).ToList(),Procurement=config.Procurement,
                Subcontractors=config.Subcontractors.Count==0?null:config.Subcontractors.Select(x=>x.CreateRuntimeData()).ToList(),Subcontracting=config.Subcontracting,Risks=config.Risks,
                Regions=config.Regions.Count==0?null:config.Regions.Select(x=>x.CreateRuntimeData()).ToList(),Expansion=expansion,Market=config.Market,
                Research=config.ResearchDefinitions.Count==0?null:config.ResearchDefinitions.Select(x=>x.CreateRuntimeData()).ToList(),ResearchSettings=config.Research,Development=config.Development};
            return ConstructionEmpire.Systems.Expansion.CareerSetup.Apply(origin,career??new ConstructionEmpire.Data.Expansion.CareerOptions {Seed=configuration.Seed});
        }
        private void Update()
        {
            if (Simulation == null || IsLoading) return;
            try
            {
                // This is the ONLY frame adapter. Systems receive one fixed game-hour context.
                Simulation.Advance(UnityEngine.Time.unscaledDeltaTime);
                autosaveElapsed += UnityEngine.Time.unscaledDeltaTime;
                if(Management!=null && autosaveElapsed>=autosaveIntervalSeconds)
                {
                    autosaveElapsed=0;
                    if(GameTime.TotalHours!=lastAutosaveHour)
                    {
                        try {SaveGame(0);lastAutosaveHour=GameTime.TotalHours;}
                        catch(Exception storageFailure){Status("Autosave failed: "+storageFailure.Message+". Use a manual slot before quitting.");}
                    }
                }
            }
            catch (Exception exception)
            {
                // A system failure cannot safely be rolled back; stop instead of silently diverging.
                Simulation.Time.Speed = SimulationSpeed.Pause;
                enabled = false;
                Debug.LogException(exception, this);
            }
        }

        [ContextMenu("Save Game")]
        public void SaveCompany()=>SaveGame(1);
        [ContextMenu("Load Game")]
        public void LoadCompany()=>LoadGame(1);
        public void SaveGame(int slot)
        {
            if(Management==null || IsLoading)throw new InvalidOperationException("No playable session is ready to save.");
            var save=Management.Commands.Capture();save.PresentationJson=CapturePresentation?.Invoke();
            SessionSaveService.Save(SlotPath(slot),save);Status((slot==0?"Saved autosave":"Saved slot "+slot)+" at game hour "+GameTime.TotalHours+". Previous save kept as backup.");
        }
        public void LoadGame(int slot,bool backup=false)
        {
            if(IsLoading)throw new InvalidOperationException("A game is already loading.");
            var save=SessionSaveService.Load(SlotPath(slot,backup));
            ValidatePresentation?.Invoke(save.PresentationJson);
            GameTime.Speed=SimulationSpeed.Pause;IsLoading=true;Status("Validating saved game…");StartCoroutine(Restore(save));
        }
        private IEnumerator Restore(SessionSaveData save)
        {
            SessionReplay replay=null;Exception failure=null;
            try{replay=new SessionReplay(save);}catch(Exception e){failure=e;}
            if(failure!=null){IsLoading=false;Status("Load failed: "+failure.Message);yield break;}
            try
            {
                while(!replay.Complete)
                {
                    try{replay.Step(256);}catch(Exception e){failure=e;}
                    if(failure!=null)break;
                    Status("Restoring saved game · "+replay.Progress.ToString("P0"));yield return null;
                }
                if(failure==null)
                {
                    Management?.Dispose();Simulation?.Dispose();Management=replay.Commit();Simulation=Management.Simulation;
                    LoadedPresentation=save.PresentationJson;IsLoading=false;GameEvents.Publish(new SimulationReplacedEvent(this));
                    Status("Game loaded and paused at hour "+GameTime.TotalHours+".");
                }
                else {IsLoading=false;Status("Load failed; current game preserved: "+failure.Message);}
            }
            finally{replay.Dispose();IsLoading=false;}
        }
        public void NewGame(ConstructionEmpire.Data.Expansion.CareerOptions career=null)
        {
            if(IsLoading)throw new InvalidOperationException("Wait for loading to finish.");
            var domain=new GameEvents.Domain();ManagementSession next;
            using(domain.Activate())next=SessionFactory.Create(CreateOrigin(career));
            Management?.Dispose();Simulation?.Dispose();Management=next;Simulation=next.Simulation;Simulation.Time.Speed=SimulationSpeed.Pause;
            LoadedPresentation=null;domain.Attach();GameEvents.Publish(new SimulationReplacedEvent(this));Status("New company ready.");
        }

        private void OnDestroy()
        {
            Management?.Dispose();
            Simulation?.Dispose();
            if (activeDriver == this) activeDriver = null;
        }
    }
}
