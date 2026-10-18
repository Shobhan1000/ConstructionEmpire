using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Data.Market;
using ConstructionEmpire.Definitions;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Simulation;
using ConstructionEmpire.UI;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.Tests.EditorIntegration
{
    public sealed class SiteVisualControllerTests
    {
        private GameObject owner;private SimulationManager simulation;private ManagementSession session;
        [SetUp]public void Setup()
        {GameEvents.Clear();owner=new GameObject("Site visual test");simulation=new SimulationManager(new CompanyData("Visuals",100000),new GameTimeData(),42);var projects=SampleProjectDataFactory.CreateStarterProjects();session=new ManagementSession(simulation,projects,SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment(),projects.ToDictionary(x=>x.Id,x=>SampleRenovationPhaseFactory.CreateHouseRenovationPhases()),SampleProjectDataFactory.CreateRates(),SampleProjectDataFactory.CreateBiddingConfiguration(),SampleProjectDataFactory.CreateCompetitors(),ManagementSettings.CreateStarter(),marketSettings:new MarketSettings {OpportunityBaseCount=0,CompetitorBidChance=0});}
        [TearDown]public void Cleanup(){Object.DestroyImmediate(owner);session.Dispose();simulation.Dispose();GameEvents.Clear();}
        [Test]public void ControllerFiltersProjectsReflectsStateAndUnsubscribesWhenDisabled()
        {
            var c=owner.AddComponent<SiteVisualController>();var host=new VisualElement();var p=session.Projects[0];p.ContractValue=100000;p.Status=ProjectStatus.Won;session.StartProject(p.Id);c.Initialize(session,p.Id,host);Assert.That(c.CurrentStageIndex,Is.Zero);
            foreach(var phase in session.Phases(p.Id))phase.Progress=45;
            GameEvents.Publish(new PhaseProgressChangedEvent("unrelated-project","other",99));Assert.That(c.CurrentStageIndex,Is.Zero);
            GameEvents.Publish(new PhaseProgressChangedEvent(p.Id,session.Phases(p.Id)[0].Id,45));Assert.That(c.CurrentStageIndex,Is.EqualTo(2));Assert.That(c.DisplayedProgress,Is.EqualTo(45).Within(1e-8));Assert.That(host.Q<Label>("site-stage").text,Does.Contain("Framed"));
            c.enabled=false;foreach(var phase in session.Phases(p.Id))phase.Progress=90;GameEvents.Publish(new PhaseProgressChangedEvent(p.Id,"phase",90));Assert.That(c.CurrentStageIndex,Is.EqualTo(2));c.enabled=true;Assert.That(c.CurrentStageIndex,Is.EqualTo(4));
        }
        [Test]public void PresenceBadgesTrackAssignmentsAndHideAfterReleaseWithoutMutation()
        {var c=owner.AddComponent<SiteVisualController>();var host=new VisualElement();var p=session.Projects[0];c.Initialize(session,p.Id,host);Assert.That(host.Q("site-workers").style.display.value,Is.EqualTo(DisplayStyle.None));session.Workforce.Workers[0].CurrentProjectId=p.Id;session.Workforce.Equipment[0].CurrentProjectId=p.Id;GameEvents.Publish(new ResourcesChangedEvent(p.Id));Assert.That(c.AssignedWorkerCount,Is.EqualTo(1));Assert.That(c.AssignedEquipmentCount,Is.EqualTo(1));Assert.That(session.Progress(p),Is.Zero);session.Workforce.Workers[0].CurrentProjectId=null;session.Workforce.Equipment[0].CurrentProjectId=null;GameEvents.Publish(new ResourcesChangedEvent(p.Id));Assert.That(c.AssignedWorkerCount,Is.Zero);Assert.That(host.Q("site-equipment").style.display.value,Is.EqualTo(DisplayStyle.None));}
        [Test]public void StagePrefabIsReusedAndOwnedClonesAreRemovedOnDestroy()
        {
            var definition=ScriptableObject.CreateInstance<SiteVisualDefinition>();var prototype=new GameObject("Empty visual placeholder");var anchor=new GameObject("Visual anchor");
            try{definition.Stages[1].Prefab=prototype;var c=owner.AddComponent<SiteVisualController>();c.Definition=definition;c.PrefabRoot=anchor.transform;var p=session.Projects[0];c.Initialize(session,p.Id);foreach(var phase in session.Phases(p.Id))phase.Progress=25;c.Refresh();Assert.That(anchor.transform.childCount,Is.EqualTo(1));c.Refresh();Assert.That(anchor.transform.childCount,Is.EqualTo(1));Object.DestroyImmediate(c);Assert.That(anchor.transform.childCount,Is.Zero);foreach(var phase in session.Phases(p.Id))phase.Progress=0;GameEvents.Publish(new ResourcesChangedEvent(p.Id));}
            finally{Object.DestroyImmediate(anchor);Object.DestroyImmediate(prototype);Object.DestroyImmediate(definition);}
        }
        [Test]public void MissingProjectHidesViewAndRebindingReadsAnotherProject()
        {var c=owner.AddComponent<SiteVisualController>();var host=new VisualElement();c.Initialize(session,"missing",host);Assert.That(c.CurrentStageIndex,Is.EqualTo(-1));Assert.That(host.Q("site-visual").style.display.value,Is.EqualTo(DisplayStyle.None));c.Initialize(session,session.Projects[1].Id,host);Assert.That(host.childCount,Is.EqualTo(1));Assert.That(c.CurrentStageIndex,Is.Zero);}
    }
}
