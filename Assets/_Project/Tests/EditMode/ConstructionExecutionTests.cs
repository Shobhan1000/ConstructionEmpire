using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Persistence;
using ConstructionEmpire.Systems.Simulation;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class ConstructionExecutionTests
    {
        [SetUp] public void SetUp() => GameEvents.Clear();
        [TearDown] public void TearDown() => GameEvents.Clear();
        private static CrewEfficiencyConfiguration Neutral() => new CrewEfficiencyConfiguration {
            SkillFloor=1,UnmatchedTradeMultiplier=1,ExperienceBonusPerYear=0,MaximumExperienceBonus=0,
            NoSupervisorMultiplier=1,SupervisorSkillBonus=0,MaximumFatiguePenalty=0 };
        private static CrewData Crew(string id="crew")
        {
            var employee=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment().Workers[0];
            employee.Id=id+"-worker"; employee.Productivity=1;
            return new CrewData {Id=id,Name=id,Members=new List<EmployeeData>{employee}};
        }
        private static ConstructionPhaseRuntimeData Phase(string id="phase",double duration=2) => new ConstructionPhaseRuntimeData {
            Id=id,Name=id,DurationHours=duration,LaborHours=duration,Cost=100 };
        private static ProjectData Project(string id="project")
        {
            var project=SampleProjectDataFactory.CreateStarterProjects()[0]; project.Id=id; project.Status=ProjectStatus.Won; return project;
        }
        private static TickContext Tick(long hour=1) => new TickContext(hour,24,7,30);

        [Test] public void DependencyGraph_BlocksFoundationUntilExcavationCompletes()
        {
            var excavation=Phase("excavation"); var foundation=Phase("foundation"); foundation.PrerequisiteIds.Add("excavation");
            var graph=new PhaseDependencyGraph(new[]{foundation,excavation});
            Assert.That(graph.TopologicalOrder,Is.EqualTo(new[]{"excavation","foundation"}));
            Assert.That(graph.CanStart("foundation"),Is.False);
            excavation.Progress=99; Assert.That(graph.CanStart("foundation"),Is.False);
            excavation.Progress=100; Assert.That(graph.CanStart("foundation"),Is.True);
        }
        [Test] public void DependencyGraph_RejectsCyclesMissingAndDuplicateIds()
        {
            var a=Phase("a"); var b=Phase("b"); a.PrerequisiteIds.Add("b"); b.PrerequisiteIds.Add("a");
            Assert.Throws<ArgumentException>(()=>new PhaseDependencyGraph(new[]{a,b}));
            Assert.Throws<ArgumentException>(()=>new PhaseDependencyGraph(new[]{a}));
            Assert.Throws<ArgumentException>(()=>new PhaseDependencyGraph(new[]{b,b}));
        }
        [Test] public void ProgressFormula_IsExactNamedMultiplierChain()
        {
            var factors=new ProgressFactors(10,.8,.5,.75,.9,.6);
            Assert.That(factors.Progress,Is.EqualTo(1.62).Within(1e-12));
            Assert.That(new ProgressFactors(10,1,1,0,1,1).Progress,Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ProgressFactors(double.NaN,1,1,1));
        }
        [Test] public void CrewEfficiency_ExposesEveryFactorAndResourcesAreNotDoubled()
        {
            var crew=Crew(); var phase=Phase(); var config=Neutral();
            crew.Members[0].Skill=.5; crew.Members[0].Experience=5; crew.Members[0].Fatigue=.25;
            config.SkillFloor=0; config.ExperienceBonusPerYear=.1; config.MaximumExperienceBonus=1;
            config.NoSupervisorMultiplier=.8; config.MaximumFatiguePenalty=1;
            var result=CrewEfficiencyCalculator.Calculate(crew,phase,config,.5,.5);
            Assert.That(result.LaborEfficiency,Is.EqualTo(.5*1.5*.8*.75).Within(1e-12));
            Assert.That(result.Efficiency,Is.EqualTo(result.LaborEfficiency*.5*.5).Within(1e-12));
            Assert.That(crew.Morale,Is.EqualTo(.8)); Assert.That(crew.CostPerHour,Is.EqualTo(18));
        }
        [Test] public void Materials_ConsumeProportionallyThenStallAndResume()
        {
            var engine=new ProjectProgressEngine(Neutral()); var project=Project(); var phase=Phase();
            phase.MaterialRequirements.Add(new MaterialQuantity("timber",10));
            var inventory=new MaterialInventoryData(); inventory.Add("timber",2);
            var crew=Crew(); engine.StartProject(project,new[]{phase},inventory); engine.AssignCrew(project.Id,phase.Id,crew,Array.Empty<EquipmentRuntimeData>());
            engine.OnTick(Tick()); Assert.That(phase.Progress,Is.EqualTo(20).Within(1e-10)); Assert.That(inventory.GetQuantity("timber"),Is.Zero.Within(1e-10));
            engine.OnTick(Tick(2)); Assert.That(phase.Progress,Is.EqualTo(20).Within(1e-10));
            inventory.Add("timber",8); engine.OnTick(Tick(3)); engine.OnTick(Tick(4));
            Assert.That(phase.Progress,Is.EqualTo(100)); Assert.That(project.Status,Is.EqualTo(ProjectStatus.Complete));
            Assert.That(crew.Members[0].CurrentProjectId,Is.Null);
        }
        [Test] public void MissingEquipmentAndEmptyFuel_BlockWork()
        {
            var engine=new ProjectProgressEngine(Neutral()); var project=Project(); var phase=Phase();
            phase.EquipmentRequirements.Add(new EquipmentRequirement(EquipmentType.SmallExcavator,1));
            engine.StartProject(project,new[]{phase},new MaterialInventoryData());
            var crew=Crew(); engine.AssignCrew(project.Id,phase.Id,crew,Array.Empty<EquipmentRuntimeData>());
            engine.OnTick(Tick()); Assert.That(phase.Progress,Is.Zero); engine.ReleaseCrew(project.Id,phase.Id);
            var machine=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment().Equipment[1]; machine.CurrentFuel=0;
            engine.AssignCrew(project.Id,phase.Id,crew,new[]{machine}); engine.OnTick(Tick(2)); Assert.That(phase.Progress,Is.Zero);
            machine.CurrentFuel=2; engine.OnTick(Tick(3)); Assert.That(phase.Progress,Is.EqualTo(25).Within(1e-10)); Assert.That(machine.CurrentFuel,Is.Zero.Within(1e-10));
        }
        [Test] public void Completion_PublishesOnceAndCostsOnlyActualWork()
        {
            var engine=new ProjectProgressEngine(Neutral()); var project=Project(); var phase=Phase(duration:.25); var crew=Crew();
            int completed=0; GameEvents.Subscribe<ProjectCompletedEvent>(e=> { Assert.That(e.ProjectId,Is.EqualTo(project.Id)); completed++; });
            engine.StartProject(project,new[]{phase},new MaterialInventoryData()); engine.AssignCrew(project.Id,phase.Id,crew,Array.Empty<EquipmentRuntimeData>());
            engine.OnTick(Tick()); engine.OnTick(Tick(2));
            Assert.That(completed,Is.EqualTo(1)); Assert.That(phase.Progress,Is.EqualTo(100)); Assert.That(phase.AccruedCost,Is.EqualTo(104.5).Within(1e-10));
        }
        [Test] public void Assignment_BlocksPrerequisitesAndDuplicateWorkers()
        {
            var engine=new ProjectProgressEngine(Neutral()); var project=Project(); var a=Phase("a"); var b=Phase("b"); var c=Phase("c"); b.PrerequisiteIds.Add("a");
            var crew=Crew(); engine.StartProject(project,new[]{a,b,c},new MaterialInventoryData());
            Assert.Throws<InvalidOperationException>(()=>engine.AssignCrew(project.Id,"b",crew,Array.Empty<EquipmentRuntimeData>()));
            engine.AssignCrew(project.Id,"a",crew,Array.Empty<EquipmentRuntimeData>());
            Assert.Throws<InvalidOperationException>(()=>engine.AssignCrew(project.Id,"c",crew,Array.Empty<EquipmentRuntimeData>()));
        }
        [Test] public void ParallelAssignedPhases_BothAdvanceAndFinalEventWaitsForAll()
        {
            var engine=new ProjectProgressEngine(Neutral()); var project=Project(); var a=Phase("a",1); var b=Phase("b",2);
            engine.StartProject(project,new[]{a,b},new MaterialInventoryData()); engine.AssignCrew(project.Id,"a",Crew("one"),Array.Empty<EquipmentRuntimeData>());
            engine.AssignCrew(project.Id,"b",Crew("two"),Array.Empty<EquipmentRuntimeData>());
            engine.OnTick(Tick()); Assert.That(a.Progress,Is.EqualTo(100)); Assert.That(b.Progress,Is.EqualTo(50)); Assert.That(project.Status,Is.EqualTo(ProjectStatus.Active));
            engine.OnTick(Tick(2)); Assert.That(project.Status,Is.EqualTo(ProjectStatus.Complete));
        }
        [Test] public void SimulationManager_DrivesConstructionAtEverySpeed()
        {
            foreach(var speed in new[]{SimulationSpeed.Normal,SimulationSpeed.Double,SimulationSpeed.Fast,SimulationSpeed.Faster,SimulationSpeed.Maximum})
            {
                using(var simulation=new SimulationManager(new CompanyData("Test",100000),new GameTimeData {Speed=speed},42,Neutral()))
                {
                    var phase=Phase(duration:100); var project=Project(); simulation.Construction.StartProject(project,new[]{phase},new MaterialInventoryData());
                    simulation.Construction.AssignCrew(project.Id,phase.Id,Crew(),Array.Empty<EquipmentRuntimeData>());
                    int ticks=simulation.Advance(1);
                    Assert.That(phase.Progress,Is.EqualTo(ticks).Within(1e-10));
                    simulation.Time.Speed=SimulationSpeed.Pause; simulation.Advance(10); Assert.That(phase.Progress,Is.EqualTo(ticks).Within(1e-10));
                }
            }
        }
        [Test] public void StarterRosterAndPhases_CompleteFullRenovationThroughCentralDriver()
        {
            var starter=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment();
            Assert.That(starter.Workers.Count,Is.EqualTo(6)); Assert.That(starter.Workers.Count(w=>w.WorkerType==WorkerType.SiteSupervisor),Is.EqualTo(1));
            Assert.That(starter.Equipment.Count,Is.EqualTo(2));
            var phases=SampleRenovationPhaseFactory.CreateHouseRenovationPhases(); var graph=new PhaseDependencyGraph(phases);
            var inventory=new MaterialInventoryData(); foreach(var phase in phases) foreach(var material in phase.MaterialRequirements) inventory.Add(material.MaterialId,material.Quantity);
            var crew=new CrewData {Id="starter",Name="Starter Crew",Members=starter.Workers}; var project=Project();
            int completed=0; GameEvents.Subscribe<ProjectCompletedEvent>(e=>completed++);
            using(var simulation=new SimulationManager(new CompanyData("Test",100000),new GameTimeData(),42))
            {
                simulation.Construction.StartProject(project,phases,inventory);
                foreach(var id in graph.TopologicalOrder)
                {
                    simulation.Construction.AssignCrew(project.Id,id,crew,starter.Equipment);
                    var phase=phases.Find(p=>p.Id==id);
                    for(int tick=0;tick<1000 && !phase.IsComplete;tick++)
                    { foreach(var machine in starter.Equipment) machine.CurrentFuel=machine.FuelCapacity; simulation.Advance(1); }
                    Assert.That(phase.IsComplete,Is.True,id);
                }
            }
            Assert.That(project.Status,Is.EqualTo(ProjectStatus.Complete)); Assert.That(completed,Is.EqualTo(1));
        }
        [Test] public void NewSaveDtos_MigrateOneVersionAtATime()
        {
            var starter=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment();
            var data=new ConstructionEmpire.Data.Save.SaveDataBase[] {starter.Workers[0],starter.Equipment[0],new MaterialInventoryData(),Crew(),Phase()};
            foreach(var item in data) { SaveMigrationRunner.Run(item,_=>{}); Assert.That(item.SchemaVersion,Is.EqualTo(2)); }
        }
    }
}
