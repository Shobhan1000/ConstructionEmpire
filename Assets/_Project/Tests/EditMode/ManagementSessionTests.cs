using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class ManagementSessionTests
    {
        [SetUp] public void Setup()=>GameEvents.Clear();
        [TearDown] public void Cleanup()=>GameEvents.Clear();
        private static SimulationManager Simulation()=>new SimulationManager(new CompanyData("Test",250000),new GameTimeData(),42);
        private static ManagementSession Session(SimulationManager simulation)
        {
            var projects=SampleProjectDataFactory.CreateStarterProjects();
            return new ManagementSession(simulation,projects,SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment(),
                projects.ToDictionary(x=>x.Id,x=>SampleRenovationPhaseFactory.CreateHouseRenovationPhases()),SampleProjectDataFactory.CreateRates(),
                SampleProjectDataFactory.CreateBiddingConfiguration(),SampleProjectDataFactory.CreateCompetitors(),ManagementSettings.CreateStarter(),riskSettings:new ConstructionEmpire.Data.Risk.RiskSettings {WeatherShiftRate=0,BreakdownRate=0,AbsenceRate=0,SafetyRate=0,GroundDiscoveryRate=0,FatiguePerWorkHour=0,EquipmentWearPerHour=0,InspectionBaseFailure=0,InspectionVulnerabilityWeight=0});
        }
        [Test] public void ScheduledEvents_AreOrderedCancelableAndDoNotFireEarly()
        {
            var queue=new ScheduledEventQueue(); var seen=new List<int>(); GameEvents.Subscribe<int>(seen.Add);
            queue.Schedule(3,3); queue.Schedule(2,1); queue.Schedule(2,2); var cancel=queue.Schedule(1,99); Assert.That(queue.Cancel(cancel),Is.True);
            queue.OnTick(new TickContext(1,24,7,28)); Assert.That(seen,Is.Empty);
            queue.OnTick(new TickContext(2,24,7,28)); CollectionAssert.AreEqual(new[]{1,2},seen);
            queue.OnTick(new TickContext(3,24,7,28)); CollectionAssert.AreEqual(new[]{1,2,3},seen);
            Assert.Throws<ArgumentOutOfRangeException>(()=>queue.Schedule(3,4));
        }
        [Test] public void MaterialOrder_ChargesOnceAndDeliversOnFixedHour()
        {
            using(var sim=Simulation()) using(var session=Session(sim))
            {
                int deliveries=0; GameEvents.Subscribe<MaterialDeliveredEvent>(_=>deliveries++);
                var order=session.OrderMaterial(session.Projects[0].Id,"concrete",2);
                Assert.That(sim.Company.CashBalance,Is.EqualTo(250000-order.Cost).Within(.01));
                sim.Advance(3); Assert.That(session.Inventory.GetQuantity("concrete"),Is.Zero);
                sim.Advance(1); Assert.That(session.Inventory.GetQuantity("concrete"),Is.EqualTo(2));
                sim.Advance(1); Assert.That(deliveries,Is.EqualTo(1)); Assert.That(order.Delivered,Is.True);
            }
        }
        [Test] public void InvalidOrdersAreRejectedButFinanceTracksPurchasesBeyondAvailableCash()
        {
            using(var sim=Simulation()) using(var session=Session(sim))
            {
                Assert.Throws<ArgumentOutOfRangeException>(()=>session.OrderMaterial(session.Projects[0].Id,"concrete",double.NaN));
                sim.Company.ApplyTransaction(-250000,"Empty cash");
                session.OrderMaterial(session.Projects[0].Id,"concrete",1);
                Assert.That(session.Finance.CashFlow.IsOverdrawn,Is.True);
                Assert.That(session.Orders.Count,Is.EqualTo(1)); Assert.That(session.Inventory.Stock,Is.Empty);
            }
        }
        [Test] public void PricesReplayDeterministicallyWithSameSeedAndTicks()
        {
            double[] first;
            using(var sim=Simulation()) using(var session=Session(sim)) { sim.Advance(48); first=session.Materials.Select(x=>x.Price).ToArray(); }
            using(var sim=Simulation()) using(var session=Session(sim)) { sim.Advance(48); CollectionAssert.AreEqual(first,session.Materials.Select(x=>x.Price).ToArray()); }
        }
        [Test] public void FullPlayableConstruction_SettlesOnceAndReleasesCrew()
        {
            using(var sim=Simulation()) using(var session=Session(sim))
            {
                var project=session.Projects[0]; project.Status=ProjectStatus.Won; project.ContractValue=80000; session.StartProject(project.Id);
                foreach(var group in session.Phases(project.Id).SelectMany(x=>x.MaterialRequirements).GroupBy(x=>x.MaterialId)) session.OrderMaterial(project.Id,group.Key,group.Sum(x=>x.Quantity)+.000001);
                sim.Advance(4);
                var graph=new ConstructionEmpire.Systems.Construction.PhaseDependencyGraph(session.Phases(project.Id));
                int completions=0; GameEvents.Subscribe<ProjectCompletedEvent>(_=>completions++);
                foreach(string phaseId in graph.TopologicalOrder)
                {
                    session.AssignCrew(project.Id,phaseId);
                    var phase=session.Phases(project.Id).Single(x=>x.Id==phaseId);
                    for(int tick=0;tick<1000 && !phase.IsComplete;tick++) { foreach(var machine in session.Workforce.Equipment) session.Refuel(machine.Id); sim.Advance(1); }
                    Assert.That(phase.IsComplete,Is.True,phaseId);
                }
                Assert.That(project.Status,Is.EqualTo(ProjectStatus.Complete)); Assert.That(session.Revenue,Is.EqualTo(80000));
                Assert.That(sim.Company.Reputation,Is.EqualTo(55)); Assert.That(session.Crew.CurrentProjectId,Is.Null); Assert.That(completions,Is.EqualTo(1));
                double revenue=session.Revenue; sim.Advance(5); Assert.That(session.Revenue,Is.EqualTo(revenue));
            }
        }
        [Test] public void OriginalBriefDefaultsAndAllSixteenPhasesArePresent()
        {
            Assert.That(SimulationDefaults.StartingCash,Is.EqualTo(250000));
            var phases=SampleRenovationPhaseFactory.CreateHouseRenovationPhases(); Assert.That(phases.Count,Is.EqualTo(16));
            var drywall=phases.Single(x=>x.Id=="drywall"); Assert.That(drywall.PrerequisiteIds,Does.Contain("insulation"));
            var insulation=phases.Single(x=>x.Id=="insulation"); Assert.That(insulation.PrerequisiteIds,Does.Contain("electrical").And.Contain("plumbing"));
        }
        [Test] public void ReputationMutation_PublishesCommittedValueOnce()
        {
            var company=new CompanyData("Test",100); int count=0;
            GameEvents.Subscribe<ReputationChangedEvent>(e=> { Assert.That(company.Reputation,Is.EqualTo(e.CurrentReputation)); count++; });
            company.SetReputation(45,"Test"); company.SetReputation(45,"No change"); Assert.That(count,Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>company.SetReputation(float.NaN,"Test"));
        }
    }
}
