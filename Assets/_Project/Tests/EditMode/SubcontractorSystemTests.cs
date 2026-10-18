using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Data.Subcontractors;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Finance;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
using ConstructionEmpire.Systems.Subcontractors;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class SubcontractorSystemTests
    {
        private SimulationManager simulation;private FinanceSystem finance;private SubcontractorSystem system;
        private ProjectData project;private ConstructionPhaseRuntimeData phase;private MaterialInventoryData inventory;
        [SetUp] public void Setup()
        {
            GameEvents.Clear();simulation=new SimulationManager(new CompanyData("Contracting",100000),new GameTimeData(),713);
            finance=new FinanceSystem(simulation.Company,simulation.Time,new FinanceSettings {OfficeCostPerDay=0,InsurancePremium=0,TaxRate=0,ProjectOverheadPerWorkHour=0});simulation.Register(finance);
            system=new SubcontractorSystem(SampleSubcontractorDataFactory.CreateStarterSubcontractors(),simulation.Construction,finance,simulation.Time,simulation.Random,new SubcontractorSettings {QuotePriceVariation=0,QuoteTimelineVariation=0,DelayHours=3});
            project=SampleProjectDataFactory.CreateStarterProjects()[0];project.Status=ProjectStatus.Won;
            phase=new ConstructionPhaseRuntimeData {Id="electrical",Name="Electrical",DurationHours=2,LaborHours=2,RequiresSubcontractor=true,SubcontractorSpecialty=SubcontractorSpecialty.Electrical};
            inventory=new MaterialInventoryData();simulation.Construction.StartProject(project,new[]{phase},inventory);
        }
        [TearDown] public void Cleanup(){system.Dispose();finance.Dispose();simulation.Dispose();GameEvents.Clear();}
        [Test] public void LowReliabilityHasExpectedDelayFrequencyAndExactSeedReplay()
        {
            var first=new SeededRandom(718);var second=new SeededRandom(718);int delays=0;
            for(int i=0;i<10000;i++){bool a=SubcontractorPerformance.RollDelay(.2,first);Assert.That(SubcontractorPerformance.RollDelay(.2,second),Is.EqualTo(a));if(a)delays++;}
            Assert.That(delays/10000d,Is.InRange(.78,.82));
            Assert.That(SubcontractorPerformance.RollDelay(0,first),Is.True);Assert.That(SubcontractorPerformance.RollDelay(1,first),Is.False);
        }
        [Test] public void QuotesCompeteAndRepeatedRequestsPreservePricesAndRandomState()
        {
            var quotes=system.RequestQuotes(project.Id,phase);Assert.That(quotes.Count,Is.EqualTo(2));
            Assert.That(quotes.Select(x=>x.CompanyId).Distinct().Count(),Is.EqualTo(2));Assert.That(quotes.All(x=>x.Price>0 && x.DurationHours>0),Is.True);
            var repeated=system.RequestQuotes(project.Id,phase);Assert.That(repeated[0],Is.SameAs(quotes[0]));Assert.That(quotes[0].Price,Is.Not.EqualTo(quotes[1].Price));
            var control=new SeededRandom(713);for(int i=0;i<4;i++)control.NextDouble();Assert.That(simulation.Random.NextDouble(),Is.EqualTo(control.NextDouble()));
        }
        [Test] public void DelayAndMaterialShortageBlockTheSamePhaseThenCompletionReleasesCapacity()
        {
            foreach(var c in system.Companies)c.Reliability=0;
            phase.MaterialRequirements.Add(new MaterialQuantity("wire",10));int delayed=0,completed=0;
            GameEvents.Subscribe<ProjectDelayedEvent>(_=>delayed++);GameEvents.Subscribe<ProjectCompletedEvent>(_=>completed++);
            var quote=system.RequestQuotes(project.Id,phase)[0];var company=system.Companies.Single(x=>x.Id==quote.CompanyId);
            var award=system.Award(quote.Id,Array.Empty<EquipmentRuntimeData>());
            Assert.That(delayed,Is.EqualTo(1));Assert.That(phase.Risk,Is.EqualTo(1));Assert.That(company.CanAcceptWork,Is.False);
            simulation.Advance(3);Assert.That(phase.Progress,Is.Zero);Assert.That(award.Delayed,Is.True);
            simulation.Advance(1);Assert.That(phase.Progress,Is.Zero,"Delivery shortage still blocks a team after its delay ends.");
            inventory.Add("wire",10);simulation.Advance(quote.DurationHours);
            Assert.That(phase.IsComplete,Is.True);Assert.That(award.Completed,Is.True);Assert.That(company.CanAcceptWork,Is.True);Assert.That(completed,Is.EqualTo(1));
            simulation.Advance(3);Assert.That(completed,Is.EqualTo(1));Assert.That(inventory.GetQuantity("wire"),Is.Zero.Within(1e-8));
            Assert.That(finance.Ledger.Count(x=>x.Category==FinancialCategory.Subcontractors && x.Kind==LedgerKind.Expense),Is.EqualTo(1));Assert.That(finance.Expenses,Is.EqualTo(quote.Price));
        }
        [Test] public void DuplicateAwardAndInternalCrewCannotBypassSpecialtyRequirement()
        {
            var crew=new CrewData {Id="crew",Name="Crew",Members=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment().Workers};
            Assert.Throws<InvalidOperationException>(()=>simulation.Construction.AssignCrew(project.Id,phase.Id,crew,Array.Empty<EquipmentRuntimeData>()));
            var quotes=system.RequestQuotes(project.Id,phase);system.Award(quotes[0].Id,Array.Empty<EquipmentRuntimeData>());
            Assert.Throws<InvalidOperationException>(()=>system.Award(quotes[1].Id,Array.Empty<EquipmentRuntimeData>()));Assert.That(system.Awards.Count,Is.EqualTo(1));
        }
        [Test] public void PrerequisitesMustCompleteBeforeAwardAndFailureDoesNotReserveCompany()
        {
            var next=SampleProjectDataFactory.CreateStarterProjects()[1];next.Status=ProjectStatus.Won;
            var prerequisite=new ConstructionPhaseRuntimeData {Id="first",Name="First",DurationHours=1,LaborHours=1};var dependent=phase.Copy();dependent.PrerequisiteIds.Add("first");
            simulation.Construction.StartProject(next,new[]{prerequisite,dependent},inventory);var quotes=system.RequestQuotes(next.Id,dependent);
            Assert.Throws<InvalidOperationException>(()=>system.Award(quotes[0].Id,Array.Empty<EquipmentRuntimeData>()));Assert.That(system.Awards.Count,Is.Zero);Assert.That(system.Companies.All(x=>x.CanAcceptWork),Is.True);
            prerequisite.Progress=100;Assert.DoesNotThrow(()=>system.Award(quotes[0].Id,Array.Empty<EquipmentRuntimeData>()));
        }
        [Test] public void SubcontractedWorkUsesNormalWeatherAndMaterialMultipliers()
        {
            foreach(var c in system.Companies)c.Reliability=1;
            phase.MaterialRequirements.Add(new MaterialQuantity("wire",10));inventory.Add("wire",10);
            var quote=system.RequestQuotes(project.Id,phase)[0];system.Award(quote.Id,Array.Empty<EquipmentRuntimeData>());
            simulation.Construction.SetEnvironment(project.Id,.5,.5);simulation.Advance(1);
            Assert.That(phase.Progress,Is.EqualTo(100d/quote.DurationHours*.5*.5).Within(1e-8));
            Assert.That(inventory.GetQuantity("wire"),Is.EqualTo(10-phase.Progress/10).Within(1e-8));
        }
        [Test] public void StarterCompaniesCoverAllSixSpecialtiesWithTwoDifferentProfilesEach()
        {
            var companies=SampleSubcontractorDataFactory.CreateStarterSubcontractors();Assert.That(companies.Count,Is.EqualTo(12));
            foreach(SubcontractorSpecialty specialty in Enum.GetValues(typeof(SubcontractorSpecialty)))
            {var pair=companies.Where(x=>x.Specialty==specialty).ToList();Assert.That(pair.Count,Is.EqualTo(2));Assert.That(pair[0].Reliability,Is.Not.EqualTo(pair[1].Reliability));foreach(var c in pair)c.Validate();}
        }
    }
}

