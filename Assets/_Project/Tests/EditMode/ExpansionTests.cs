using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Expansion;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class ExpansionTests
    {
        private SimulationManager sim;
        [SetUp]public void Setup(){GameEvents.Clear();sim=new SimulationManager(new CompanyData("Expansion",250000),new GameTimeData(),42,new CrewEfficiencyConfiguration {SkillFloor=1,UnmatchedTradeMultiplier=1,ExperienceBonusPerYear=0,MaximumExperienceBonus=0,NoSupervisorMultiplier=1,SupervisorSkillBonus=0,MaximumFatiguePenalty=0});}
        [TearDown]public void Cleanup(){sim.Dispose();GameEvents.Clear();}
        private static ConstructionPhaseRuntimeData Phase()=>new ConstructionPhaseRuntimeData {Id="work",Name="Work",DurationHours=10,LaborHours=10};
        private static CrewData Crew(string id){var worker=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment().Workers[0];worker.Id=id+"-worker";worker.Productivity=1;return new CrewData {Id=id,Name=id,Members=new List<EmployeeData>{worker}};}
        private static ProjectData Start(ProjectProgressEngine engine,int index,ConstructionPhaseRuntimeData phase){var p=SampleProjectDataFactory.CreateStarterProjects()[index];p.Status=ProjectStatus.Won;engine.StartProject(p,new[]{phase},new MaterialInventoryData());return p;}
        private ManagementSession Session(){var projects=SampleProjectDataFactory.CreateStarterProjects();return new ManagementSession(sim,projects,SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment(),projects.ToDictionary(x=>x.Id,x=>SampleRenovationPhaseFactory.CreateHouseRenovationPhases()),SampleProjectDataFactory.CreateRates(),SampleProjectDataFactory.CreateBiddingConfiguration(),SampleProjectDataFactory.CreateCompetitors(),ManagementSettings.CreateStarter());}
        [Test]public void ConcurrentProjectsKeepProgressEnvironmentAndScheduleIndependent()
        {
            var other=sim.CreateProjectEngine();var a=Phase();var b=Phase();var p=Start(sim.Construction,0,a);var q=Start(other,1,b);
            sim.Construction.AssignCrew(p.Id,a.Id,Crew("a"),Array.Empty<EquipmentRuntimeData>());other.AssignCrew(q.Id,b.Id,Crew("b"),Array.Empty<EquipmentRuntimeData>());
            other.SetEnvironment(q.Id,1,0);sim.Advance(2);Assert.That(a.Progress,Is.EqualTo(20));Assert.That(b.Progress,Is.Zero);
            other.SetEnvironment(q.Id,1,.5);sim.Advance(2);Assert.That(a.Progress,Is.EqualTo(40));Assert.That(b.Progress,Is.EqualTo(10));Assert.That(q.Schedule.Phases[0].ActualStart,Is.EqualTo(2));
        }
        [Test]public void SharedPhaseObjectsAreRejectedBeforeChangingSecondProject()
        {var other=sim.CreateProjectEngine();var phase=Phase();Start(sim.Construction,0,phase);var q=SampleProjectDataFactory.CreateStarterProjects()[1];q.Status=ProjectStatus.Won;Assert.Throws<InvalidOperationException>(()=>other.StartProject(q,new[]{phase},new MaterialInventoryData()));Assert.That(q.Status,Is.EqualTo(ProjectStatus.Won));Assert.That(other.HasProjects,Is.False);}
        [Test]public void BookingRegistryCoversAllThreeEnginesAndResourceCopies()
        {
            var middle=sim.CreateProjectEngine();var last=sim.CreateProjectEngine();var p=Start(sim.Construction,0,Phase());Start(middle,1,Phase());var r=Start(last,2,Phase());var crew=Crew("a");var machine=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment().Equipment[0];
            sim.Construction.ScheduleCrew(p.Id,"work",crew,new[]{machine},0,5);
            Assert.Throws<InvalidOperationException>(()=>last.ScheduleCrew(r.Id,"work",crew,Array.Empty<EquipmentRuntimeData>(),1,4));
            Assert.Throws<InvalidOperationException>(()=>last.ScheduleCrew(r.Id,"work",Crew("b"),new[]{machine.Copy()},1,4));Assert.That(last.Scheduling,Is.SameAs(middle.Scheduling));
        }
        [Test]public void AdjacentBookingHandoversDoNotDependOnEngineTickOrder()
        {
            var later=sim.CreateProjectEngine();var a=Phase();var b=Phase();var p=Start(sim.Construction,0,a);var q=Start(later,1,b);var crew=Crew("shared");
            later.ScheduleCrew(q.Id,"work",crew,Array.Empty<EquipmentRuntimeData>(),0,1);sim.Construction.ScheduleCrew(p.Id,"work",crew,Array.Empty<EquipmentRuntimeData>(),1,2);
            sim.Advance(2);Assert.That(a.Progress,Is.EqualTo(10));Assert.That(b.Progress,Is.EqualTo(10));Assert.That(crew.CurrentProjectId,Is.EqualTo(p.Id));
        }
        [Test]public void RegionGatesSupportReputationOrCapitalAndCombinedRules()
        {var r=SampleExpansionDataFactory.CreateStarterRegions()[1];Assert.That(r.MeetsThreshold(55,0),Is.True);Assert.That(r.MeetsThreshold(0,200000),Is.True);Assert.That(r.MeetsThreshold(54,199999),Is.False);r.UnlockRule=RegionUnlockRule.ReputationAndCapital;Assert.That(r.MeetsThreshold(55,0),Is.False);Assert.That(r.MeetsThreshold(55,200000),Is.True);}
        [Test]public void RegionalHiringDoesNotChangeHomeCrewOrReuseWorkers()
        {using(var s=Session()){var home=s.Crew.Members.ToArray();var o=s.Expansion.OpenOffice("riverside");var crew=s.Expansion.HireRegionalCrew(o.Id);CollectionAssert.AreEqual(home,s.Crew.Members);Assert.That(crew.Members.Count,Is.EqualTo(6));Assert.That(s.Workforce.Workers.Count,Is.EqualTo(12));Assert.That(home.Select(x=>x.Id).Intersect(crew.Members.Select(x=>x.Id)),Is.Empty);Assert.That(sim.ProjectEngines.Count,Is.EqualTo(2));Assert.That(o.RegionalWorkforcePool,Is.Empty);}}
        [Test]public void LockedRegionsDoNotMutateOfficeOrCashState()
        {using(var s=Session()){var cash=sim.Company.CashBalance;Assert.Throws<InvalidOperationException>(()=>s.Expansion.OpenOffice("hillcrest"));Assert.That(s.Expansion.Offices.Count,Is.EqualTo(1));Assert.That(sim.Company.CashBalance,Is.EqualTo(cash));}}
        [Test]public void RegionalOpportunitiesUseAllowedTypesAndIndependentPhaseCopies()
        {using(var s=Session()){var o=s.Expansion.OpenOffice("riverside");var generated=s.Expansion.GenerateOpportunities(o.Id);Assert.That(generated.Count,Is.EqualTo(3));Assert.That(generated.All(x=>x.Region=="Riverside" && s.Expansion.Region(o.RegionId).ProjectTypes.Contains(x.BuildingType)),Is.True);var a=s.Phases(generated[0].Id)[0];var b=s.Phases(generated[1].Id)[0];a.Progress=33;Assert.That(b.Progress,Is.Zero);Assert.That(s.Expansion.CompetitorsFor(generated[0]).Count,Is.EqualTo(3));Assert.That(s.Expansion.DeliveryCost(generated[0]),Is.GreaterThan(0));Assert.That(s.Expansion.DeliveryHours(generated[0]),Is.GreaterThan(0));}}
        [Test]public void RegionCopiesDoNotShareUnlockPools()
        {var region=SampleExpansionDataFactory.CreateStarterRegions()[0];var copy=region.Copy();copy.ProjectTypes.Clear();copy.CompetitorIds.Clear();Assert.That(region.ProjectTypes,Is.Not.Empty);Assert.That(region.CompetitorIds,Is.Not.Empty);}
    }
}
