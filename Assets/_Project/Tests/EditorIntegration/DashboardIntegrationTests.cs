using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.Tests.EditorIntegration
{
    public sealed class DashboardIntegrationTests
    {
        private GameObject owner;
        [SetUp] public void Setup() { GameEvents.Clear(); owner=new GameObject("Notification test"); }
        [TearDown] public void Cleanup() { if(owner!=null) Object.DestroyImmediate(owner); GameEvents.Clear(); }
        [Test] public void UxmlContainsDashboardDetailAndNavigationAnchors()
        {
            var asset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/Dashboard/Dashboard.uxml"); Assert.That(asset,Is.Not.Null);
            var root=asset.CloneTree();
            foreach(string name in new[]{"navigation","metrics","active-projects","financial-chart","alerts","opportunities","detail-tabs","detail-content","confirmation"}) Assert.That(root.Q(name),Is.Not.Null,name);
            Assert.That(MainDashboardController.Screens.Length,Is.EqualTo(19));
        }
        [Test] public void NotificationsKeepHistoryNavigateAndUnsubscribeOnDestroy()
        {
            var center=owner.AddComponent<NotificationCenter>(); center.Initialize();
            int added=0; GameEvents.Subscribe<NotificationAddedEvent>(_=>added++);
            GameEvents.Publish(new EquipmentBreakdownEvent("excavator-1","starter-1","Hydraulic fault"));
            GameEvents.Publish(new MaterialDelayedEvent("order-1","starter-1",2,"Traffic"));
            Assert.That(center.Notifications.Count,Is.EqualTo(2)); Assert.That(center.Notifications[0].Severity,Is.EqualTo(NotificationSeverity.Critical));
            string selected=null; center.NavigateToNotification=n=>selected=n.RelatedEntityId; center.Navigate(center.Notifications[0]); Assert.That(selected,Is.EqualTo("excavator-1"));
            Object.DestroyImmediate(owner); owner=null;
            GameEvents.Publish(new EquipmentBreakdownEvent("excavator-1","starter-1","Again")); Assert.That(added,Is.EqualTo(2));
        }
        [Test] public void AllRequiredAlertTypesProduceTheirRelatedEntity()
        {
            var center=owner.AddComponent<NotificationCenter>(); center.Initialize();
            GameEvents.Publish(new BudgetExceededEvent("p",100,101)); GameEvents.Publish(new WorkerAbsentEvent("w","p",8,"Absent"));
            GameEvents.Publish(new InspectionFailedEvent("p","i","Failed"));
            Assert.That(center.Notifications.Select(x=>x.RelatedEntityId),Is.EquivalentTo(new[]{"p","w","i"}));
        }
    }
}
