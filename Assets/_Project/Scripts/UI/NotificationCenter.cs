using System;
using System.Linq;
using System.Collections.Generic;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Data.Events;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    public enum NotificationSeverity { Info, Healthy, Warning, Critical }
    [Serializable]
    public sealed class NotificationRecord
    {
        public string Id,Title,Description,RelatedEntityId,ProjectId,Screen;
        public long TimestampHour;
        public NotificationSeverity Severity;
    }
    [ExecuteAlways] // No editor simulation; ensures manually initialized editor listeners also clean up.
    [DisallowMultipleComponent]
    public sealed class NotificationCenter : MonoBehaviour
    {
        public UIDocument Document;
        public SimulationBootstrapper Bootstrap;
        private readonly List<NotificationRecord> notifications=new List<NotificationRecord>();
        private EventSubscriptions subscriptions;
        private long sequence;
        public IReadOnlyList<NotificationRecord> Notifications => notifications.AsReadOnly();
        // A UI can replace navigation with its own router without changing notification storage.
        public Action<NotificationRecord> NavigateToNotification { get; set; }
        public void Initialize()
        {
            if(subscriptions!=null) return;
            subscriptions=new EventSubscriptions();
            subscriptions.Subscribe<ConstructionEmpire.Data.Research.ResearchAvailableEvent>(e=>{if(ReferenceEquals(e.Owner,Bootstrap?.Management?.Research.System))Add(NotificationSeverity.Info,"Research available",Bootstrap.Management.Research.System.Upgrades.Single(x=>x.Id==e.UpgradeId).Name+" is ready to research.",e.UpgradeId,"Research");});
            subscriptions.Subscribe<ConstructionEmpire.Data.Research.ResearchChangedEvent>(e=>{if(e.Status=="Completed" && ReferenceEquals(e.Owner,Bootstrap?.Management?.Research.System))Add(NotificationSeverity.Healthy,"Research completed",Bootstrap.Management.Research.System.Upgrades.Single(x=>x.Id==e.UpgradeId).Name+" effects are now active.",e.UpgradeId,"Research");});
            subscriptions.Subscribe<ConstructionEmpire.Data.Market.CompetitorClaimedContractEvent>(e=>{if(!e.PlayerSubmitted)Add(NotificationSeverity.Info,"Contract awarded to competitor","Lost to "+e.CompetitorName+". The client selected their proposal.",e.CompetitorId,"Bidding",e.ProjectId);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Market.CompetitorDevelopmentEvent>(e=>Add(NotificationSeverity.Info,"Competitor news",e.Description,e.CompetitorId,"Company"));
            subscriptions.Subscribe<ConstructionEmpire.Data.Risk.WeatherChangedEvent>(e=>{if(Bootstrap?.Management!=null)foreach(var p in Bootstrap.Management.Projects)if(p.Region==e.Region && p.Status==ConstructionEmpire.Data.Bidding.ProjectStatus.Active)Add(NotificationSeverity.Info,"Weather shift",e.Weather+" in "+e.Region+". Outdoor work is more exposed than indoor work.",p.Id,"Project Detail",p.Id);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Risk.SiteConditionDiscoveredEvent>(e=>Add(NotificationSeverity.Warning,"Unexpected ground conditions",e.Description,e.ProjectId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<ConstructionEmpire.Data.Risk.InspectionPassedEvent>(e=>Add(NotificationSeverity.Healthy,"Inspection passed",e.PhaseId+" checkpoint accepted.",e.PhaseId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<ConstructionEmpire.Data.Scheduling.ResourceBookingConflictEvent>(e=>Add(NotificationSeverity.Warning,"Resource scheduling conflict",e.Description,e.PhaseId,"Schedule",e.ProjectId));
            subscriptions.Subscribe<CashFlowWarningEvent>(e=> { if(ReferenceEquals(e.Company,Bootstrap?.Company)) Add(e.Critical ? NotificationSeverity.Critical : NotificationSeverity.Warning,e.Critical ? "Cash deficit" : "Low cash", "Cash on hand: "+DashboardElements.Money((double)e.Cash)+". Review payment dates and available credit.",null,"Finance"); });
            subscriptions.Subscribe<ProfitabilityWarningEvent>(e=>Add(NotificationSeverity.Critical,"Forecast loss", "Forecast costs exceed the contract value.",e.ProjectId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<BudgetExceededEvent>(e=>Add(NotificationSeverity.Critical,"Budget exceeded",$"Budget {DashboardElements.Money(e.Budget)}; incurred {DashboardElements.Money(e.ActualCost)}.",e.ProjectId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<EquipmentBreakdownEvent>(e=>Add(NotificationSeverity.Critical,"Equipment breakdown",e.Reason,e.EquipmentId,"Equipment",e.ProjectId));
            subscriptions.Subscribe<MaterialDelayedEvent>(e=>Add(NotificationSeverity.Warning,"Material delivery delayed",e.Reason+" Delay: "+e.DelayHours+" hours.",e.OrderId,"Materials",e.ProjectId));
            subscriptions.Subscribe<WorkerAbsentEvent>(e=>Add(NotificationSeverity.Warning,"Worker absent",e.Reason,e.EmployeeId,"Employees",e.ProjectId));
            subscriptions.Subscribe<InspectionFailedEvent>(e=>Add(NotificationSeverity.Critical,"Inspection failed",e.Reason,e.InspectionId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<ReputationChangedEvent>(e=> { if(ReferenceEquals(e.Company,Bootstrap?.Company) && e.CurrentReputation<e.PreviousReputation) Add(NotificationSeverity.Warning,"Reputation decreased",e.Reason,null,"Company"); });
            subscriptions.Subscribe<BidWonEvent>(e=>Add(NotificationSeverity.Healthy,"Project awarded","Your bid was accepted. Open the project to plan resources.",e.ProjectId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<BidLostEvent>(e=>Add(NotificationSeverity.Info,"Bid not selected",e.Reason,e.ProjectId,"Bidding",e.ProjectId));
            subscriptions.Subscribe<PaymentReceivedEvent>(e=>Add(NotificationSeverity.Healthy,"Payment received",DashboardElements.Money(e.Amount)+" received.",e.PaymentId,"Finance",e.ProjectId));
            subscriptions.Subscribe<ProjectDelayedEvent>(e=>Add(NotificationSeverity.Warning,"Schedule alert",e.Reason,e.ProjectId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<ProjectCompletedEvent>(e=>Add(NotificationSeverity.Healthy,"Project completed","All construction phases are complete.",e.ProjectId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<SafetyIncidentEvent>(e=>Add(NotificationSeverity.Critical,"Safety incident",e.Description,e.IncidentId,"Project Detail",e.ProjectId));
            subscriptions.Subscribe<MaterialDeliveredEvent>(e=>Add(NotificationSeverity.Info,"Materials delivered",e.Quantity+" units of "+e.MaterialId+" received.",e.OrderId,"Materials",e.ProjectId));
            subscriptions.Subscribe<TransactionRejectedEvent>(e=> { if(ReferenceEquals(e.Company,Bootstrap?.Company)) Add(NotificationSeverity.Critical,"Insufficient cash",e.Reason+" could not be paid. Construction may be paused.",null,"Finance"); });
        }
        private void Add(NotificationSeverity severity,string title,string description,string entity,string screen,string project=null)
        {
            var record=new NotificationRecord {Id="notification-"+(++sequence),Severity=severity,Title=title,Description=description,
                RelatedEntityId=entity,Screen=screen,ProjectId=project,TimestampHour=Bootstrap?.GameTime?.TotalHours??0};
            notifications.Add(record);if(notifications.Count>10000)notifications.RemoveAt(0); GameEvents.Publish(new NotificationAddedEvent(record.Id));
        }
        public void Restore(IEnumerable<NotificationRecord> saved)
        {notifications.Clear();if(saved!=null)notifications.AddRange(saved.Where(x=>x!=null).Take(10000));sequence=notifications.Select(x=>long.TryParse((x.Id??"").Replace("notification-",""),out var n)?n:0).DefaultIfEmpty(0).Max();}
        public void Navigate(NotificationRecord record)
        {
            if(record==null) return;
            if(NavigateToNotification!=null) NavigateToNotification(record);
            else GameEvents.Publish(new NavigationRequestedEvent(record.Screen,record.ProjectId??record.RelatedEntityId));
        }
        public VisualElement CreateRow(NotificationRecord record)
        {
            var box=new VisualElement(); box.AddToClassList("notification"); box.AddToClassList(record.Severity.ToString().ToLowerInvariant());
            box.focusable=true; box.tooltip=record.Description+" Related entity: "+(record.RelatedEntityId??"company");
            DashboardElements.Label(box,record.Title,"notification-title");
            DashboardElements.Label(box,record.Description,"notification-description");
            DashboardElements.Label(box,$"Hour {record.TimestampHour} · {record.Severity}","muted");
            box.RegisterCallback<ClickEvent>(_=>Navigate(record));
            box.RegisterCallback<KeyDownEvent>(e=> { if(e.keyCode==KeyCode.Return) Navigate(record); }); return box;
        }
        private void OnDestroy() { subscriptions?.Dispose(); NavigateToNotification=null; }
    }
}
