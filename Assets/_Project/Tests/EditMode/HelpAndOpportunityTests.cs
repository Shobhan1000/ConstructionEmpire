using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Expansion;
using ConstructionEmpire.Data.Presentation;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Expansion;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Persistence;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class HelpAndOpportunityTests
    {
        [Test]public void ContextHelpPrioritizesTheMetricOverCrossReferencedConcepts()
        {
            Assert.That(HelpCatalog.ForControl("CASH","Cash is available now; a profitable project may still be unable to pay bills.","Dashboard").Id,Is.EqualTo("cash"));
            Assert.That(HelpCatalog.ForControl("RETENTION","Contract money withheld until the warranty period ends.","Finance").Id,Is.EqualTo("retention"));
            Assert.That(HelpCatalog.ForControl("","","Crews").Id,Is.EqualTo("crews"));
            Assert.That(HelpCatalog.Match("current"),Is.Null,"Do not confuse current with rent.");
        }
        [Test]public void GeneratedOpportunityCommandsSurviveSaveReplayAndRespectCompanyLevel()
        {
            GameEvents.Clear();var projects=SampleProjectDataFactory.CreateStarterProjects();
            var origin=new SessionStartData {Company=new CompanyData("Generator test",250000),Clock=new GameTimeData(),Seed=927,Projects=projects,Workforce=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment(),Phases=projects.ToDictionary(x=>x.Id,x=>SampleRenovationPhaseFactory.CreateHouseRenovationPhases()),Rates=SampleProjectDataFactory.CreateRates(),Bidding=SampleProjectDataFactory.CreateBiddingConfiguration(),Competitors=SampleProjectDataFactory.CreateCompetitors(),Management=ManagementSettings.CreateStarter(),Market=new MarketSettings {CompetitorBidChance=0,OpportunityBaseCount=0},Expansion=new ExpansionSettings {ProceduralOpportunities=true}};
            var session=SessionFactory.Create(origin);
            try
            {
                session.Commands.Execute(new SessionCommandData {Kind=SessionCommandKind.GenerateOpportunities,Id=session.Expansion.Offices[0].Id});
                Assert.That(session.Projects.Count,Is.EqualTo(8));var unlocked=origin.Expansion.OpportunityProfiles.Where(p=>p.MinimumLevel<=session.Development.Level).ToList();
                foreach(var project in session.Projects.Skip(5))Assert.That(unlocked.Any(p=>project.Title.StartsWith(p.Title,StringComparison.Ordinal)),Is.True);
                var save=session.Commands.Capture();using(var replay=new SessionReplay(save)){int guard=0;while(!replay.Step(17))if(++guard>1000)Assert.Fail("Replay timed out");Assert.That(SessionStateProof.Compute(replay.Session),Is.EqualTo(save.StateProof));}
            }
            finally{session.Dispose();session.Simulation.Dispose();GameEvents.Clear();}
        }
        [Test]public void EveryManagementScreenHasRelevantHelpAndSearchFindsCashTiming()
        {
            var screens=new[]{"Dashboard","Projects","Project Detail","Bidding","Estimates","Schedule","Employees","Crews","Equipment","Materials","Suppliers","Subcontractors","Clients","Finance","Company","Research","World Map","Notifications","Settings"};
            foreach(var screen in screens){var topic=HelpCatalog.Resolve("",screen);Assert.That(topic.Body.Length,Is.GreaterThan(100));if(screen!="Dashboard")Assert.That(topic.Id,Is.Not.EqualTo("dashboard"),screen);}
            Assert.That(HelpCatalog.Search("cash").Any(x=>x.Id=="cash"),Is.True);
            Assert.That(HelpCatalog.Resolve("Retention withheld","Finance").Id,Is.EqualTo("retention"));
            Assert.That(HelpCatalog.Topics.Select(x=>x.Id).Distinct().Count(),Is.EqualTo(HelpCatalog.Topics.Count));
        }
        [Test]public void AllThirtyTwoProfilesProduceValidIndependentTaskGraphs()
        {
            var templates=SampleProjectDataFactory.CreateStarterProjects();var phases=SampleRenovationPhaseFactory.CreateHouseRenovationPhases();var original=SessionJson.Serialize(phases);
            var profiles=OpportunityProfileCatalog.Create();Assert.That(profiles.Count,Is.EqualTo(32));
            foreach(var profile in profiles)
            {
                var source=templates.First(x=>x.BuildingType==profile.BuildingType);var before=SessionJson.Serialize(source);
                var generated=OpportunityGenerator.Create(source,phases,profile,new SeededRandom(42),100,1);
                Assert.That(new PhaseDependencyGraph(generated.Phases).TopologicalOrder.Count,Is.EqualTo(generated.Phases.Count));
                Assert.That(generated.Project.DeadlineHour,Is.GreaterThan(100));Assert.That(generated.Project.Status,Is.EqualTo(ProjectStatus.Available));
                Assert.That(SessionJson.Serialize(source),Is.EqualTo(before));
                generated.Phases[0].Progress=99;Assert.That(SessionJson.Serialize(phases),Is.EqualTo(original));
            }
        }
        [Test]public void SameSeedReplaysTheFullGeneratedBriefAndWorkload()
        {
            var template=SampleProjectDataFactory.CreateStarterProjects()[0];var phases=SampleRenovationPhaseFactory.CreateHouseRenovationPhases();var profile=OpportunityProfileCatalog.Create()[0];
            var a=OpportunityGenerator.Create(template,phases,profile,new SeededRandom(934),200,1.1);
            var b=OpportunityGenerator.Create(template,phases,profile,new SeededRandom(934),200,1.1);
            Assert.That(SessionJson.Serialize(a.Project),Is.EqualTo(SessionJson.Serialize(b.Project)));Assert.That(SessionJson.Serialize(a.Phases),Is.EqualTo(SessionJson.Serialize(b.Phases)));
        }
        [Test]public void ScalingChangesResourcesBudgetAndDeadlineWithoutMutatingTemplate()
        {
            var source=SampleProjectDataFactory.CreateStarterProjects()[0];var phases=SampleRenovationPhaseFactory.CreateHouseRenovationPhases();
            var profile=new OpportunityProfile {Id="double",Title="Double workload",BuildingType="Residential",MinimumScale=2,MaximumScale=2,FocusMultiplier=1,DeadlineFactor=1};
            var result=OpportunityGenerator.Create(source,phases,profile,new SeededRandom(4),100,1);
            Assert.That(result.Phases.Sum(x=>x.LaborHours),Is.EqualTo(phases.Sum(x=>x.LaborHours)*2).Within(.001));Assert.That(result.Project.Budget,Is.EqualTo(source.Budget*2));Assert.That(result.Project.DeadlineHour,Is.EqualTo(source.DeadlineHour*2+100));
        }
        [Test]public void InteriorRefitOmitsGroundworksAndReconnectsSurvivingDependencies()
        {
            var result=OpportunityGenerator.Create(SampleProjectDataFactory.CreateStarterProjects()[0],SampleRenovationPhaseFactory.CreateHouseRenovationPhases(),OpportunityProfileCatalog.Create()[0],new SeededRandom(4),0,1);
            Assert.That(result.Phases.Any(x=>x.Id=="excavation" || x.Id=="foundation"),Is.False);
            Assert.That(result.Project.Requirements.EquipmentHours,Is.Zero,"No excavation means no excavator allowance in this refit's estimate.");
            Assert.That(result.Phases.Single(x=>x.Id=="exterior-walls").PrerequisiteIds,Does.Contain("site-preparation"));
        }
    }
}
