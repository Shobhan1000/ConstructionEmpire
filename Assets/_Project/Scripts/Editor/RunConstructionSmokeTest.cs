using System;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Simulation;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class RunConstructionSmokeTest
    {
        [MenuItem("Tools/Debug/Run Construction Smoke Test")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying) { Debug.Log("Enter Play Mode to run the construction smoke test."); return; }
            var project=SampleProjectDataFactory.CreateStarterProjects()[0]; project.Id="construction-smoke"; project.Status=ProjectStatus.Won;
            var roster=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment();
            var phases=SampleRenovationPhaseFactory.CreateHouseRenovationPhases();
            var crew=new CrewData {Id="smoke-crew",Name="Smoke Test Crew",Members=roster.Workers};
            var inventory=new MaterialInventoryData();
            foreach(var phase in phases) foreach(var material in phase.MaterialRequirements) inventory.Add(material.MaterialId,material.Quantity);
            int completionEvents=0;
            Action<ProjectCompletedEvent> listener=e=> {if(e.ProjectId==project.Id) completionEvents++;};
            GameEvents.Subscribe(listener);
            try
            {
                using(var simulation=new SimulationManager(new CompanyData("Construction smoke test",100000),new GameTimeData(),42))
                {
                    simulation.Construction.StartProject(project,phases,inventory);
                    foreach(var id in new PhaseDependencyGraph(phases).TopologicalOrder)
                    {
                        simulation.Construction.AssignCrew(project.Id,id,crew,roster.Equipment);
                        var phase=phases.Find(p=>p.Id==id);
                        for(int i=0;i<1000 && !phase.IsComplete;i++)
                        {
                            // Explicit test replenishment; procurement and fuel purchasing are later systems.
                            foreach(var machine in roster.Equipment) machine.CurrentFuel=machine.FuelCapacity;
                            simulation.Advance(1);
                        }
                        if(!phase.IsComplete) throw new InvalidOperationException("Smoke test stalled at "+id);
                    }
                    if(project.Status!=ProjectStatus.Complete || completionEvents!=1 || crew.Members.Any(w=>w.CurrentProjectId!=null))
                        throw new InvalidOperationException("Construction completion or resource release failed.");
                    Debug.Log($"Construction smoke test PASSED: {phases.Count} phases completed through SimulationManager, one completion event, crews released, {simulation.Time.CaptureState().TotalHours} fixed hours.");
                }
            }
            finally { GameEvents.Unsubscribe(listener); }
        }
    }
}
