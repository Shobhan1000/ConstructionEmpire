using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Scheduling;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Scheduling;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class SchedulingSystemTests
    {
        private static ConstructionPhaseRuntimeData Phase(string id,double hours,params string[] prerequisites)=>new ConstructionPhaseRuntimeData {Id=id,Name=id,DurationHours=hours,LaborHours=hours,PrerequisiteIds=prerequisites.ToList()};
        private SimulationManager sim;private SchedulingSystem scheduling;private CrewData crew;private EquipmentRuntimeData machine;
        [SetUp]public void Setup()
        {
            GameEvents.Clear();var config=new CrewEfficiencyConfiguration {SkillFloor=1,UnmatchedTradeMultiplier=1,ExperienceBonusPerYear=0,MaximumExperienceBonus=0,NoSupervisorMultiplier=1,SupervisorSkillBonus=0,MaximumFatiguePenalty=0};
            sim=new SimulationManager(new CompanyData("Schedules",250000),new GameTimeData(),42,config);scheduling=new SchedulingSystem();sim.Construction.EnableScheduling(scheduling);
            var roster=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment();var worker=roster.Workers[0];worker.Productivity=1;
            crew=new CrewData {Id="crew",Name="Crew",Members=new List<EmployeeData>{worker}};machine=roster.Equipment[0];
        }
        [TearDown]public void Cleanup(){sim.Dispose();GameEvents.Clear();}
        private ProjectData Start(int index,params ConstructionPhaseRuntimeData[] phases)
        {var p=SampleProjectDataFactory.CreateStarterProjects()[index];p.Status=ProjectStatus.Won;sim.Construction.StartProject(p,phases,new MaterialInventoryData(),sim.Time.TotalHours);return p;}
        [Test]public void ForkJoinCriticalPathHasKnownDatesAndFloat()
        {
            var tasks=new[]{Phase("A",2),Phase("B",5,"A"),Phase("C",2,"A"),Phase("D",3,"B","C")};
            var schedule=CriticalPathCalculator.Calculate("p",tasks,24);
            CollectionAssert.AreEqual(new[]{"A","B","D"},schedule.Phases.Where(x=>x.IsCriticalPath).Select(x=>x.PhaseId));
            Assert.That(schedule.PlannedCompletion,Is.EqualTo(34));var c=schedule.Phases.Single(x=>x.PhaseId=="C");Assert.That(c.PlannedStart,Is.EqualTo(26));Assert.That(c.TotalFloat,Is.EqualTo(3));
            Assert.That(schedule.Phases.Single(x=>x.PhaseId=="D").PlannedStart,Is.EqualTo(31));
        }
        [Test]public void EqualParallelBranchesAreBothCriticalAndCyclesAreRejected()
        {
            var tasks=new[]{Phase("A",2),Phase("B",2),Phase("C",1,"A","B")};var schedule=CriticalPathCalculator.Calculate("p",tasks);
            Assert.That(schedule.Phases.All(x=>x.IsCriticalPath),Is.True);tasks[0].PrerequisiteIds.Add("C");Assert.Throws<ArgumentException>(()=>CriticalPathCalculator.Calculate("p",tasks));
        }
        [Test]public void OverlappingCrewReservationsAcrossProjectsAreFlaggedAndRejected()
        {
            var p=Start(0,Phase("a",10));var q=Start(1,Phase("b",10));int conflicts=0;GameEvents.Subscribe<ResourceBookingConflictEvent>(_=>conflicts++);
            sim.Construction.ScheduleCrew(p.Id,"a",crew,Array.Empty<EquipmentRuntimeData>(),2,5);
            Assert.Throws<InvalidOperationException>(()=>sim.Construction.ScheduleCrew(q.Id,"b",crew,Array.Empty<EquipmentRuntimeData>(),4,6));
            Assert.That(conflicts,Is.EqualTo(1));Assert.That(scheduling.Bookings.Count,Is.EqualTo(1));
            Assert.DoesNotThrow(()=>sim.Construction.ScheduleCrew(q.Id,"b",crew,Array.Empty<EquipmentRuntimeData>(),5,7));
        }
        [Test]public void EquipmentIdentityAndSharedWorkersDetectConflictsAcrossDifferentCrews()
        {
            var p=Start(0,Phase("a",10));var q=Start(1,Phase("b",10));
            var different=crew.Members[0].Copy();different.Id="different";var other=new CrewData {Id="other",Name="Other",Members=new List<EmployeeData>{different}};
            sim.Construction.ScheduleCrew(p.Id,"a",crew,new[]{machine},0,5);
            Assert.Throws<InvalidOperationException>(()=>sim.Construction.ScheduleCrew(q.Id,"b",other,new[]{machine.Copy()},1,4));
            other.Members=new List<EmployeeData>{crew.Members[0]};Assert.Throws<InvalidOperationException>(()=>sim.Construction.ScheduleCrew(q.Id,"b",other,Array.Empty<EquipmentRuntimeData>(),1,4));
        }
        [Test]public void WindowStartsAtBoundaryPausesOnExpiryAndActualDatesRemainAccurate()
        {
            var phase=Phase("a",4);var p=Start(0,phase);var first=sim.Construction.ScheduleCrew(p.Id,"a",crew,Array.Empty<EquipmentRuntimeData>(),2,4);
            sim.Advance(2);Assert.That(phase.Progress,Is.Zero);sim.Advance(2);Assert.That(phase.Progress,Is.EqualTo(50));Assert.That(p.Schedule.Phases[0].ActualStart,Is.EqualTo(2));
            sim.Advance(1);Assert.That(phase.Progress,Is.EqualTo(50));Assert.That(first.Data.Status,Is.EqualTo(BookingStatus.Expired));Assert.That(crew.CurrentProjectId,Is.Null);
            sim.Construction.ScheduleCrew(p.Id,"a",crew,Array.Empty<EquipmentRuntimeData>(),5,7);sim.Advance(2);
            Assert.That(phase.IsComplete,Is.True);Assert.That(p.Schedule.Phases[0].ActualCompletion,Is.EqualTo(7));Assert.That(p.Schedule.Phases[0].CurrentDelay,Is.EqualTo(3));
            sim.Advance(3);Assert.That(p.Schedule.Phases[0].ActualCompletion,Is.EqualTo(7));Assert.That(p.Schedule.ForecastCompletion,Is.EqualTo(7));
        }
        [Test]public void CriticalDelayIsRaisedForUnassignedWorkWithoutHourlyAlertSpam()
        {
            var p=Start(0,Phase("long",10),Phase("short",2));var alerts=new List<ProjectDelayedEvent>();GameEvents.Subscribe<ProjectDelayedEvent>(alerts.Add);
            sim.Advance(3);Assert.That(alerts.Count,Is.EqualTo(1));Assert.That(alerts[0].Reason,Does.Contain("long"));
            Assert.That(p.Schedule.PlannedCompletion,Is.EqualTo(10));Assert.That(p.Schedule.ForecastCompletion,Is.EqualTo(13));Assert.That(p.Schedule.Phases.Single(x=>x.PhaseId=="short").IsCriticalPath,Is.False);
        }
        [Test]public void ConsumedFloatCanMakeFormerlyNoncriticalBranchCritical()
        {
            var a=Phase("A",10);var b=Phase("B",2);var plan=CriticalPathCalculator.Calculate("p",new[]{a,b});Assert.That(plan.Phases.Single(x=>x.PhaseId=="B").IsCriticalPath,Is.False);
            a.Progress=90;CriticalPathCalculator.Recalculate(plan,new[]{a,b},9);
            Assert.That(plan.Phases.Single(x=>x.PhaseId=="B").IsCriticalPath,Is.True);Assert.That(plan.ForecastCompletion,Is.EqualTo(11));Assert.That(plan.PlannedCompletion,Is.EqualTo(10));
        }
        [Test]public void ReservationsWaitForDependenciesAndCancellationFreesTheirWindow()
        {
            var p=Start(0,Phase("a",1),Phase("b",1,"a"));var b=sim.Construction.ScheduleCrew(p.Id,"b",crew,Array.Empty<EquipmentRuntimeData>(),0,10);sim.Advance(1);
            Assert.That(b.Data.Status,Is.EqualTo(BookingStatus.Blocked));Assert.That(crew.CurrentProjectId,Is.Null);Assert.That(p.Schedule.Phases.Single(x=>x.PhaseId=="b").ActualStart,Is.Null);
            sim.Construction.CancelBooking(b.Data.Id);Assert.DoesNotThrow(()=>sim.Construction.ScheduleCrew(p.Id,"a",crew,Array.Empty<EquipmentRuntimeData>(),1,2));sim.Advance(1);
            Assert.That(p.Schedule.Phases.Single(x=>x.PhaseId=="a").ActualCompletion,Is.EqualTo(2));
        }
        [Test]public void CopyingProjectDoesNotShareMutableScheduleRows()
        {var p=Start(0,Phase("a",2));var copy=p.Copy();copy.Schedule.Phases[0].PlannedStart=99;Assert.That(p.Schedule.Phases[0].PlannedStart,Is.Zero);}
    }
}
