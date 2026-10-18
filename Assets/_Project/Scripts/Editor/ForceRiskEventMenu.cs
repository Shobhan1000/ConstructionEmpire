using System;
using System.Linq;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Management;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class ForceRiskEventMenu
    {
        private const string Menu="Tools/Debug/Force Trigger Risk Event/";
        [MenuItem(Menu+"Weather Shift")]public static void Weather()=>Run("Weather Shift",(s,p)=>s.Risks.Weather.ForceShift(p.Region,s.Risks.Weather.Get(p.Region).Weather==WeatherKind.Storm?WeatherKind.Clear:WeatherKind.Storm), (s,p)=>true);
        [MenuItem(Menu+"Equipment Breakdown")]public static void Breakdown()=>Run("Equipment Breakdown",(s,p)=>s.Risks.Events.Breakdown(p,s.Workforce.Equipment.First(x=>x.CurrentProjectId==p.Id && x.Status==EquipmentStatus.Working)),(s,p)=>s.Workforce.Equipment.Any(x=>x.CurrentProjectId==p.Id && x.Status==EquipmentStatus.Working));
        [MenuItem(Menu+"Worker Absence")]public static void Absence()=>Run("Worker Absence",(s,p)=>s.Risks.Events.Absent(p,s.Workforce.Workers.First(x=>x.CurrentProjectId==p.Id && x.AbsentUntilHour<=s.Simulation.Time.TotalHours),s.Simulation.Time.TotalHours),(s,p)=>s.Workforce.Workers.Any(x=>x.CurrentProjectId==p.Id && x.AbsentUntilHour<=s.Simulation.Time.TotalHours));
        [MenuItem(Menu+"Safety Incident")]public static void Safety()=>Run("Safety Incident",(s,p)=>s.Risks.Events.SafetyIncident(p,s.Simulation.Time.TotalHours),(s,p)=>s.Workforce.Workers.Any(x=>x.CurrentProjectId==p.Id));
        [MenuItem(Menu+"Inspection Failure")]public static void Inspection()=>Run("Inspection Failure",(s,p)=>s.Risks.Events.FailInspection(p,s.Phases(p.Id).First(x=>!x.IsComplete && (x.Progress>0 || s.Workforce.Workers.Any(w=>w.CurrentProjectId==p.Id && w.CurrentTaskId==x.Id))),s.Simulation.Time.TotalHours),(s,p)=>s.Phases(p.Id).Any(x=>!x.IsComplete && (x.Progress>0 || s.Workforce.Workers.Any(w=>w.CurrentProjectId==p.Id && w.CurrentTaskId==x.Id))));
        private static void Run(string name,Action<ManagementSession,ProjectData> action,Func<ManagementSession,ProjectData,bool> eligible)
        {
            if(!Application.isPlaying){Debug.LogWarning(name+" requires Play Mode. No changes made.");return;}
            var session=UnityEngine.Object.FindFirstObjectByType<SimulationBootstrapper>()?.Management;
            var p=session?.Projects.FirstOrDefault(x=>x.Status==ProjectStatus.Active && eligible(session,x));
            if(p==null){Debug.LogWarning("No eligible active project/resources for "+name+". Start and staff a project first.");return;}
            try{session.Commands.MarkDiagnosticMutation("forced risk event");action(session,p);GameEvents.Publish(new NavigationRequestedEvent("Project Detail",p.Id));Debug.Log("Forced risk event: "+name+" / "+p.Title+". Probability roll bypassed; normal gameplay consequences and UI events applied.");}catch(Exception e){Debug.LogException(e);}
        }
        [MenuItem("Tools/Debug/Prepare Risk Test Project")]
        public static void Prepare()
        {
            if(!Application.isPlaying){Debug.LogWarning("Risk preview requires Play Mode. No changes made.");return;}
            var session=UnityEngine.Object.FindFirstObjectByType<SimulationBootstrapper>()?.Management;
            if(session==null){Debug.LogWarning("No active management session.");return;}
            if(session.Projects.Any(x=>x.Status==ProjectStatus.Active)){Debug.LogWarning("Use the existing active project; preview setup does not replace active work.");return;}
            var p=session.Projects.FirstOrDefault(x=>x.Status==ProjectStatus.Available);if(p==null){Debug.LogWarning("No available preview project.");return;}
            session.Commands.MarkDiagnosticMutation("risk preview setup");session.Simulation.Time.Speed=SimulationSpeed.Pause;p.Status=ProjectStatus.Won;p.ContractValue=p.Budget*1.2f;session.StartProject(p.Id);
            foreach(var group in session.Phases(p.Id).SelectMany(x=>x.MaterialRequirements).GroupBy(x=>x.MaterialId))session.Inventory.Add(group.Key,group.Sum(x=>x.Quantity));
            var first=session.Phases(p.Id).First(x=>x.PrerequisiteIds.Count==0);session.EngineFor(p.Id).AssignCrew(p.Id,first.Id,session.Crew,session.Workforce.Equipment);
            GameEvents.Publish(new ResourcesChangedEvent(p.Id));GameEvents.Publish(new NavigationRequestedEvent("Project Detail",p.Id));
            Debug.Log("Risk preview prepared in Play Mode only: project awarded, materials stocked and crew/equipment assigned; time paused. Exit Play Mode to discard preview changes.");
        }
    }
}
