using ConstructionEmpire.Data.Save;
using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Simulation.Events;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    [DisallowMultipleComponent]
    public sealed class ProjectDetailController : MonoBehaviour
    {
        public UIDocument Document;
        private MainDashboardController dashboard;
        private NotificationCenter notifications;
        private EventSubscriptions subscriptions;
        private VisualElement root, content;
        private SiteVisualController siteVisual;
        private string selectedTab="Schedule";
        private readonly Dictionary<string,VisualElement> phaseRows=new Dictionary<string,VisualElement>();
        private readonly Dictionary<string,Button> tabs=new Dictionary<string,Button>();
        public ProjectData SelectedProject { get; private set; }
        public void Initialize(MainDashboardController owner,UIDocument document,NotificationCenter center)
        {
            subscriptions?.Dispose(); dashboard=owner; Document=document; notifications=center; root=document.rootVisualElement; content=root.Q("detail-content");
            root.Q("detail-tabs").Clear(); tabs.Clear();
            foreach(string name in new[]{"Schedule","Labor","Equipment","Materials","Subcontractors","Finance","Events"})
                tabs.Add(name,DashboardElements.Button(root.Q("detail-tabs"),name,()=>ShowTab(name),"tab-button"));
            subscriptions=new EventSubscriptions();
            subscriptions.Subscribe<ConstructionEmpire.Data.Risk.ForecastChangedEvent>(e=>{if(Matches(e.Forecast.ProjectId)){UpdateSummary();if(selectedTab=="Events")RenderTab();}});
            subscriptions.Subscribe<ConstructionEmpire.Data.Scheduling.ScheduleChangedEvent>(e=> {if(Matches(e.ProjectId) && selectedTab=="Schedule") ScheduleScreenPresenter.Refresh(content,dashboard,SelectedProject);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Subcontractors.SubcontractorChangedEvent>(e=> {if(Matches(e.ProjectId)) {if(selectedTab=="Subcontractors") RenderTab();else if(selectedTab=="Schedule") UpdateSchedule();}});
            subscriptions.Subscribe<ConstructionEmpire.Data.Procurement.ProcurementChangedEvent>(e=> {if(Matches(e.ProjectId) && selectedTab=="Materials") RenderTab();});
            subscriptions.Subscribe<FinanceChangedEvent>(e=> { if(Matches(e.ProjectId) && ReferenceEquals(e.Company,dashboard.Bootstrap.Company)) { UpdateSummary(); if(selectedTab=="Finance") RenderTab(); } });
            subscriptions.Subscribe<PhaseProgressChangedEvent>(e=> { if(!Matches(e.ProjectId)) return; UpdateSummary(); if(selectedTab=="Schedule") UpdateSchedule(); else if(selectedTab=="Finance" || selectedTab=="Materials" || selectedTab=="Equipment" || selectedTab=="Labor") RenderTab(); });
            subscriptions.Subscribe<ProjectStartedEvent>(e=>Refresh(e.ProjectId));
            subscriptions.Subscribe<ProjectCompletedEvent>(e=>Refresh(e.ProjectId));
            subscriptions.Subscribe<BudgetExceededEvent>(e=> { if(Matches(e.ProjectId)) { UpdateSummary(); if(selectedTab=="Finance") RenderTab(); } });
            subscriptions.Subscribe<ProjectDelayedEvent>(e=> { if(Matches(e.ProjectId)) UpdateSummary(); });
            subscriptions.Subscribe<ResourcesChangedEvent>(e=> { if(Matches(e.ProjectId) || string.IsNullOrEmpty(e.ProjectId)) RenderTab(); });
            subscriptions.Subscribe<MaterialDeliveredEvent>(e=> { if(selectedTab=="Materials" && SelectedProject!=null) RenderTab(); });
            subscriptions.Subscribe<MaterialDelayedEvent>(e=> { if(Matches(e.ProjectId) && selectedTab=="Materials") RenderTab(); });
            subscriptions.Subscribe<EquipmentBreakdownEvent>(e=> { if(Matches(e.ProjectId) && selectedTab=="Equipment") RenderTab(); });
            subscriptions.Subscribe<WorkerAbsentEvent>(e=> { if(Matches(e.ProjectId) && selectedTab=="Labor") RenderTab(); });
            subscriptions.Subscribe<InspectionFailedEvent>(e=> { if(Matches(e.ProjectId)) UpdateSummary(); });
            subscriptions.Subscribe<MaterialPricesChangedEvent>(_=> { if(SelectedProject!=null && selectedTab=="Materials") RenderTab(); });
            subscriptions.Subscribe<PaymentReceivedEvent>(e=> { if(Matches(e.ProjectId) && selectedTab=="Finance") RenderTab(); });
            subscriptions.Subscribe<NotificationAddedEvent>(e=> { if(SelectedProject!=null && notifications.Notifications.Last().ProjectId==SelectedProject.Id) { UpdateSummary(); if(selectedTab=="Events") RenderTab(); } });
            subscriptions.Subscribe<HourlyTickEvent>(e=> { if(SelectedProject!=null && ReferenceEquals(e.Source,dashboard.Bootstrap.GameTime)) UpdateSummary(); });
        }
        private bool Matches(string id)=>SelectedProject!=null && SelectedProject.Id==id;
        private void Refresh(string id) { if(Matches(id)) { UpdateSummary(); BuildActions(); RenderTab(); } }
        public void SelectProject(ProjectData project)
        {
            SelectedProject=project; content.Clear();
            siteVisual=GetComponent<SiteVisualController>()??gameObject.AddComponent<SiteVisualController>();siteVisual.Bootstrap=dashboard.Bootstrap;siteVisual.Initialize(dashboard.Session,project?.Id,root.Q("detail-site-visual"));
            if(project==null) return;
            DashboardElements.Set(root,"detail-title",project.Title);
            DashboardElements.Set(root,"detail-identity",$"{project.ClientName} · {project.Location} · {project.BuildingType} · {project.Size:0} units · {project.QualityRequirement} quality");
            var summary=root.Q("detail-summary"); summary.Clear();
            foreach(var pair in new[]{("contract","CONTRACT"),("budget","BUDGET"),("current-cost","INCURRED COST"),("forecast","EST. FINAL COST"),("expected-profit","EST. PROFIT"),("completion","PROGRESS"),("remaining","TIME REMAINING"),("schedule","SCHEDULE"),("quality","QUALITY"),("safety","CREW SAFETY"),("issues","ISSUES"),("risk","PROJECT RISK"),("expected-date","EXPECTED COMPLETION")})
                DashboardElements.Metric(summary,"detail-"+pair.Item1,pair.Item2,"—");
            UpdateSummary(); BuildActions(); ShowTab(selectedTab);
        }
        private void UpdateSummary()
        {
            if(SelectedProject==null) return; var p=SelectedProject; var session=dashboard.Session;
            var next=ConstructionEmpire.Systems.Management.ProjectReadiness.Get(session,p);
            DashboardElements.Set(root,"detail-next",next.Title+" — "+next.Description);
            Set("contract",DashboardElements.Money(p.ContractValue)); Set("budget",DashboardElements.Money(p.Budget)); Set("current-cost",DashboardElements.Money(session.Cost(p)));
            Set("forecast",DashboardElements.Money(session.ForecastCost(p))); Set("expected-profit",DashboardElements.Money(session.ExpectedProfit(p)));
            Set("completion",session.Progress(p).ToString("0.0")+"%");
            long remaining=p.DeadlineHour-dashboard.Bootstrap.GameTime.TotalHours; Set("remaining",remaining+" hours · due "+p.DeadlineHour);
            var forecast=session.Risks.Forecasts.Get(p.Id);string schedule=forecast!=null && forecast.DelayHours>0?"Delayed":"On track";Set("schedule",schedule);DashboardElements.Status(root.Q("detail-schedule"),schedule);
            Set("risk",session.Risk(p));DashboardElements.Status(root.Q("detail-risk"),session.Risk(p));Set("expected-date",forecast==null?"—":RiskScreenPresenter.Date(forecast.ExpectedCompletionHour));
            Set("quality",p.Site.Quality.ToString("P0")+" / "+p.QualityRequirement+" target");
            var workers=session.Workforce.Workers.Where(x=>x.CurrentProjectId==p.Id).ToList(); Set("safety",workers.Count==0 ? "No crew assigned" : workers.Average(x=>x.Safety).ToString("P0"));
            Set("issues",notifications.Notifications.Count(x=>x.ProjectId==p.Id && (x.Severity==NotificationSeverity.Warning || x.Severity==NotificationSeverity.Critical))+" recorded");
        }
        private void Set(string id,string value)=>DashboardElements.Set(root,"detail-"+id,value);
        private void BuildActions()
        {
            var actions=root.Q("detail-actions"); actions.Clear(); var p=SelectedProject;
            DashboardElements.Button(actions,"Suggested next action",()=>{var next=ConstructionEmpire.Systems.Management.ProjectReadiness.Get(dashboard.Session,p);dashboard.Navigate(next.Screen,p.Id);});
            if(p.Status==ProjectStatus.Available) DashboardElements.Button(actions,"Prepare bid",()=>dashboard.Navigate("Bidding",p.Id),"primary");
            if(p.Status==ProjectStatus.Won) DashboardElements.Button(actions,"Start project",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.StartProject,Id=p.Id})),"primary");
            if(p.Status==ProjectStatus.Active) DashboardElements.Button(actions,"Release assigned crew",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.ReleaseCrew,Id=(dashboard.Session.Expansion.CrewsFor(p).FirstOrDefault()??throw new InvalidOperationException("No regional crew is assigned.")).Id})));
            DashboardElements.Label(actions,"Status: "+p.Status+" · Penalty "+DashboardElements.Money(p.PenaltyPerLateDay)+" / late day","muted");
        }
        private void ShowTab(string name)
        { selectedTab=name; foreach(var tab in tabs) tab.Value.EnableInClassList("selected",tab.Key==name); RenderTab(); }
        private void RenderTab()
        {
            if(SelectedProject==null) return; content.Clear(); phaseRows.Clear(); var session=dashboard.Session; var id=SelectedProject.Id;
            DashboardElements.Label(content,selectedTab,"card-title");
            switch(selectedTab)
            {
                case "Schedule": ScheduleScreenPresenter.Build(content,dashboard,SelectedProject); break;
                case "Labor":
                    DashboardElements.Label(content,(session.Expansion.CrewsFor(SelectedProject).FirstOrDefault()?.Name??"No regional crew")+" · "+(session.Expansion.CrewsFor(SelectedProject).FirstOrDefault()?.CostPerHour??0).ToString("C0")+" / hour · morale "+(session.Expansion.CrewsFor(SelectedProject).FirstOrDefault()?.Morale??0).ToString("P0"),"form-note");
                    foreach(var worker in session.Workforce.Workers.Where(x=>x.CurrentProjectId==id))
                        DashboardElements.Label(content,$"{worker.Name} · {worker.WorkerType} · Skill {worker.Skill:P0} · Task {worker.CurrentTaskId}","table-row");
                    if(!session.Workforce.Workers.Any(x=>x.CurrentProjectId==id)) DashboardElements.Label(content,"No crew assigned. Choose an eligible phase in Schedule.","empty-state");
                    break;
                case "Equipment": SecondaryScreenPresenter.Equipment(content,dashboard,id); break;
                case "Materials": SecondaryScreenPresenter.Materials(content,dashboard,SelectedProject); break;
                case "Finance": FinanceScreenPresenter.Project(content,dashboard,id); break;
                case "Events":
                    RiskScreenPresenter.Project(content,dashboard,SelectedProject);
                    foreach(var notification in notifications.Notifications.Where(x=>x.ProjectId==id).Reverse()) content.Add(notifications.CreateRow(notification));
                    if(content.childCount==1) DashboardElements.Label(content,"No project events recorded.","empty-state"); break;
                case "Subcontractors": SubcontractorScreenPresenter.Build(content,dashboard,SelectedProject); break;
            }
        }
        private void UpdateSchedule(){if(SelectedProject!=null)ScheduleScreenPresenter.Refresh(content,dashboard,SelectedProject);}
        private void OnDestroy()=>subscriptions?.Dispose();
    }
}
