using ConstructionEmpire.Data.Save;
using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Simulation.Events;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    [DisallowMultipleComponent]
    public sealed class MainDashboardController : MonoBehaviour
    {
        public UIDocument Document;
        public SimulationBootstrapper Bootstrap;
        public NotificationCenter Notifications;
        public ProjectDetailController ProjectDetail;
        private EventSubscriptions subscriptions;
        private VisualElement root;
        private PanelSettings runtimePanel;
        public int InterfaceScalePercent=>PlayerPrefs.GetInt("ConstructionEmpire.InterfaceScale",100);
        public void SetInterfaceScale(int percent){percent=Mathf.Clamp(percent,85,150);PlayerPrefs.SetInt("ConstructionEmpire.InterfaceScale",percent);if(runtimePanel!=null)runtimePanel.scale=Mathf.Clamp(Screen.dpi>0?Screen.dpi/96f:1.25f,1,2)*percent/100f;}
        private readonly Dictionary<string,VisualElement> projectRows=new Dictionary<string,VisualElement>();
        private readonly Dictionary<string,VisualElement> opportunityRows=new Dictionary<string,VisualElement>();
        private readonly Dictionary<string,Button> navButtons=new Dictionary<string,Button>();
        private SecondaryScreenPresenter secondary;
        private TutorialPresenter tutorial;
        private SessionMenuPresenter menu;
        private HelpCenterPresenter help;
        public string CurrentScreen=>screen;
        public void OpenHelp(string topic="start")=>help?.Open(topic);
        private Action confirmAction;
        private string screen="Dashboard";
        public string SelectedRegionId {get;set;}
        public ManagementSession Session => Bootstrap?.Management;
        public int FullRefreshCount { get; private set; }
        public static readonly string[] Screens={"Dashboard","Projects","Project Detail","Bidding","Estimates","Schedule","Employees","Crews","Equipment","Materials","Suppliers","Subcontractors","Clients","Finance","Company","Research","World Map","Notifications","Settings"};
        private void Start()
        {
            if(Document==null) Document=GetComponent<UIDocument>();
            if(Bootstrap==null) Bootstrap=FindFirstObjectByType<SimulationBootstrapper>();
            if(Notifications==null) Notifications=GetComponent<NotificationCenter>();
            if(ProjectDetail==null) ProjectDetail=GetComponent<ProjectDetailController>();
            if(Document==null || Session==null || Notifications==null || ProjectDetail==null)
            { Debug.LogError("Dashboard is not configured. Run Attach Dashboard Controllers.",this); return; }
            runtimePanel=Instantiate(Document.panelSettings);runtimePanel.scaleMode=PanelScaleMode.ConstantPixelSize;Document.panelSettings=runtimePanel;SetInterfaceScale(InterfaceScalePercent);
            root=Document.rootVisualElement;
            // Modal siblings need the same theme as the UXML's template container.
            for(var themed=root.Q("management-app");themed!=null && themed!=root;themed=themed.parent)
                for(int i=0;i<themed.styleSheets.count;i++)if(!root.styleSheets.Contains(themed.styleSheets[i]))root.styleSheets.Add(themed.styleSheets[i]);
            Notifications.Bootstrap=Bootstrap; Notifications.Initialize();
            ProjectDetail.Initialize(this,Document,Notifications);
            secondary=new SecondaryScreenPresenter(this,root.Q("secondary-content"));
            root.RegisterCallback<GeometryChangedEvent>(e=>root.EnableInClassList("compact",e.newRect.width<1200));
            BuildShell(); Subscribe(); FullRefresh(); tutorial=new TutorialPresenter(this,root.Q("tutorial-host")); Navigate("Dashboard");
            help=new HelpCenterPresenter(this,root);
            Bootstrap.CapturePresentation=CapturePresentation;Bootstrap.ValidatePresentation=ValidatePresentation;menu=new SessionMenuPresenter(this,root);
        }
        private void BuildShell()
        {
            var quick=root.Q("dashboard-actions");DashboardElements.Button(quick,"Find your next project",()=>Navigate("Projects"),"primary");DashboardElements.Button(quick,"Plan the schedule",()=>Navigate("Schedule"));DashboardElements.Button(quick,"Explore markets",()=>Navigate("World Map"));
            var ticker=DashboardElements.Label(root.Q("proactive-alerts").parent,Session.Market.Current.News,"form-note","market-ticker");ticker.tooltip="Market news changes on simulation cadence. Existing orders and fixed-rate loans retain their agreed terms.";
            var navigation=root.Q("navigation"); navigation.Clear(); navButtons.Clear();
            var search=new TextField("Find") {name="navigation-search"};search.AddToClassList("navigation-search");search.tooltip="Find a screen by name.";navigation.parent.Insert(navigation.parent.IndexOf(navigation),search);
            foreach(var group in new[]{new[]{"OVERVIEW","Dashboard","Projects","Project Detail"},new[]{"WIN WORK","Bidding","Estimates","Clients"},new[]{"DELIVER","Schedule","Employees","Crews","Equipment","Materials","Suppliers","Subcontractors"},new[]{"GROW","Finance","Company","Research","World Map"},new[]{"WORKSPACE","Notifications","Settings"}})
            {DashboardElements.Label(navigation,group[0],"nav-group-label");foreach(string name in group.Skip(1))navButtons.Add(name,DashboardElements.Button(navigation,name,()=>Navigate(name),"nav-button"));}
            search.RegisterValueChangedCallback(e=>{foreach(var pair in navButtons)pair.Value.style.display=pair.Key.IndexOf(e.newValue??"",StringComparison.OrdinalIgnoreCase)>=0?DisplayStyle.Flex:DisplayStyle.None;});
            var metrics=root.Q("metrics"); metrics.Clear();
            foreach(var pair in new[]{("cash","CASH"),("revenue","REVENUE"),("expenses","EXPENSES"),("profit","PROFIT"),("reputation","REPUTATION"),("active-count","ACTIVE PROJECTS"),("employee-count","EMPLOYEES"),("equipment-count","EQUIPMENT")})
                DashboardElements.Metric(metrics,pair.Item1,pair.Item2,"—");
            metrics.tooltip="Current session. Revenue is earned work, not cash receipts. Expenses include accrued costs; cash follows actual payment dates.";
            var controls=root.Q("time-controls"); controls.Clear();
            foreach(int value in new[]{0,1,2,5,10,25})
            {
                var button=DashboardElements.Button(controls,value==0 ? "Ⅱ" : value+"×",()=> { Bootstrap.GameTime.Speed=(SimulationSpeed)value; UpdateClock(); });
                button.name="speed-"+value; button.tooltip=value==0 ? "Pause simulation" : value+" fixed hourly ticks per real second at default configuration";
            }
            var chart=root.Q("financial-chart"); chart.Clear();
            foreach(string id in new[]{"Revenue","Expenses","Profit","Cash flow"})
            {
                var row=DashboardElements.Box(chart,"chart-row"); var labels=DashboardElements.Box(row,"chart-label-row");
                DashboardElements.Label(labels,id); DashboardElements.Label(labels,"£0",null,"chart-value-"+id);
                var bar=DashboardElements.Box(DashboardElements.Box(row,"chart-track"),"chart-bar","chart-bar-"+id);
                bar.tooltip=(id=="Cash flow"?DashboardTooltips.For("cash-flow")+" ":"")+id+": magnitude relative to the largest session total; negative values are red.";
            }
            root.Q<Button>("confirm-cancel").clicked+=CancelConfirmation;
            root.Q<Button>("confirm-submit").clicked+=Confirm;
        }
        private void Subscribe()
        {
            subscriptions=new EventSubscriptions();
            subscriptions.Subscribe<ConstructionEmpire.Data.Company.CompanyDevelopmentChangedEvent>(e=>{if(ReferenceEquals(e.Source,Session.Development) && new[]{"Company","Clients","Equipment","Crews"}.Contains(screen))secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Research.ResearchChangedEvent>(e=>{if(!ReferenceEquals(e.Owner,Session.Research.System))return;if(screen=="Research" || e.Status=="Completed" && (screen=="Estimates" || screen=="Bidding" || screen=="Crews" || screen=="Equipment"))secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Research.ResearchAvailableEvent>(e=>{if(ReferenceEquals(e.Owner,Session.Research.System) && screen=="Research")secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Market.MarketChangedEvent>(e=>{if(!ReferenceEquals(e.Snapshot,Session.Market.Current))return;Set("market-ticker",e.Snapshot.Season+" · "+e.Snapshot.State+" · "+e.Snapshot.News);if(screen=="Company" || screen=="Bidding" || screen=="Estimates" || screen=="Employees" || screen=="Crews" || screen=="Suppliers")secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Market.CompetitorClaimedContractEvent>(e=>{UpdateProject(e.ProjectId);if(screen=="Bidding" || screen=="Projects")secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Market.CompetitorDevelopmentEvent>(_=>{if(screen=="Company")secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Expansion.OpportunitiesChangedEvent>(_=>SyncOpportunities());
            subscriptions.Subscribe<ConstructionEmpire.Data.Expansion.ExpansionChangedEvent>(_=>{Set("employee-count",Session.Workforce.Workers.Count.ToString());if(screen=="World Map" || screen=="Crews" || screen=="Employees")secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Risk.ForecastChangedEvent>(e=>{UpdateProject(e.Forecast.ProjectId);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Risk.ProactiveAlertsChangedEvent>(_=>RiskScreenPresenter.Alerts(root.Q("proactive-alerts"),this));
            subscriptions.Subscribe<ConstructionEmpire.Data.Subcontractors.SubcontractorChangedEvent>(e=> {if(screen=="Subcontractors") secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Procurement.ProcurementChangedEvent>(e=> {if(screen=="Materials" || screen=="Suppliers") secondary.Render(screen);});
            subscriptions.Subscribe<ConstructionEmpire.Data.Scheduling.ScheduleChangedEvent>(e=> {if(screen=="Schedule")secondary.RefreshSchedule(e.ProjectId);});
            subscriptions.Subscribe<FinanceChangedEvent>(e=> { if(!ReferenceEquals(e.Company,Bootstrap.Company)) return; UpdateFinancials(); if(e.ProjectId!=null) UpdateProject(e.ProjectId); if(screen=="Finance") secondary.RefreshFinance();if(screen=="Research")secondary.Render(screen); });
            subscriptions.Subscribe<BalanceChangedEvent>(e=> { if(!ReferenceEquals(e.Company,Bootstrap.Company)) return; Set("cash",DashboardElements.Money(e.CurrentBalance)); UpdateFinancials(); if(screen=="Finance") secondary.RefreshFinance();if(screen=="Research")secondary.Render(screen); });
            subscriptions.Subscribe<ReputationChangedEvent>(e=> { if(ReferenceEquals(e.Company,Bootstrap.Company)) Set("reputation",e.CurrentReputation.ToString("0")+" / 100"); });
            subscriptions.Subscribe<CompanyIdentityChangedEvent>(e=> { if(ReferenceEquals(e.Company,Bootstrap.Company)) Set("company-name",e.Company.CompanyName); });
            subscriptions.Subscribe<ProjectStartedEvent>(e=>UpdateProject(e.ProjectId));
            subscriptions.Subscribe<ProjectCompletedEvent>(e=> { UpdateProject(e.ProjectId); UpdateFinancials(); });
            subscriptions.Subscribe<BudgetExceededEvent>(e=>UpdateProject(e.ProjectId));
            subscriptions.Subscribe<ProjectDelayedEvent>(e=>UpdateProject(e.ProjectId));
            subscriptions.Subscribe<BidWonEvent>(e=>UpdateProject(e.ProjectId));
            subscriptions.Subscribe<BidLostEvent>(e=>UpdateProject(e.ProjectId));
            subscriptions.Subscribe<PhaseProgressChangedEvent>(e=> { UpdateProject(e.ProjectId); UpdateFinancials(); secondary.RefreshForProject(screen,e.ProjectId); });
            subscriptions.Subscribe<PaymentReceivedEvent>(e=> { if(Session.Find(e.ProjectId)!=null) UpdateFinancials(); });
            subscriptions.Subscribe<ResourcesChangedEvent>(e=> { Set("equipment-count",Session.Workforce.Equipment.Count(x=>x.Status!=ConstructionEmpire.Data.Construction.EquipmentStatus.Retired).ToString()); UpdateFinancials(); secondary.RefreshResources(screen); });
            subscriptions.Subscribe<MaterialDeliveredEvent>(e=>secondary.RefreshResources(screen));
            subscriptions.Subscribe<MaterialPricesChangedEvent>(_=> { if(screen=="Materials" || screen=="Suppliers") secondary.Render(screen); });
            subscriptions.Subscribe<EquipmentBreakdownEvent>(_=> { if(screen=="Equipment") secondary.Render(screen); });
            subscriptions.Subscribe<WorkerAbsentEvent>(_=> { if(screen=="Employees") secondary.Render(screen); });
            subscriptions.Subscribe<NotificationAddedEvent>(e=>AddNotification(e.Id));
            subscriptions.Subscribe<NavigationRequestedEvent>(e=>Navigate(e.Screen,e.EntityId));
            subscriptions.Subscribe<HourlyTickEvent>(e=> { if(ReferenceEquals(e.Source,Bootstrap.GameTime)) UpdateClock(); });
            subscriptions.Subscribe<SimulationReplacedEvent>(e=> { if(ReferenceEquals(e.Source,Bootstrap)) { RestorePresentation(); FullRefresh(); Navigate(screen,ProjectDetail.SelectedProject?.Id); } });
        }
        private void FullRefresh()
        {
            FullRefreshCount++;
            Set("company-name",Bootstrap.Company.CompanyName);
            Set("office-label",Session.Settings.OfficeName+"\n"+Session.Settings.OfficeLocation);
            Set("cash",DashboardElements.Money(Bootstrap.Company.CashBalance)); Set("reputation",Bootstrap.Company.Reputation.ToString("0")+" / 100");
            Set("employee-count",Session.Workforce.Workers.Count.ToString()); Set("equipment-count",Session.Workforce.Equipment.Count(x=>x.Status!=ConstructionEmpire.Data.Construction.EquipmentStatus.Retired).ToString());
            root.Q("active-projects").Clear(); root.Q("opportunities").Clear(); projectRows.Clear(); opportunityRows.Clear();
            foreach(var project in Session.Projects)
            {
                AddProject(project);
            }
            root.Q("alerts").Clear(); foreach(var notification in Notifications.Notifications.TakeLast(5)) AddNotification(notification.Id);
            UpdateFinancials(); UpdateClock(); RiskScreenPresenter.Alerts(root.Q("proactive-alerts"),this);
        }
        private void SyncOpportunities()
        {foreach(var project in Session.Projects)if(!projectRows.ContainsKey(project.Id))AddProject(project);if(screen=="World Map" || screen=="Projects")secondary.Render(screen);}
        private void AddProject(ProjectData project)
        {
                var row=CreateProjectRow(project); projectRows.Add(project.Id,row); root.Q("active-projects").Add(row);
                var opportunity=DashboardElements.Box(root.Q("opportunities"),"opportunity-row"); var info=DashboardElements.Box(opportunity,"opportunity-info");
                DashboardElements.Label(info,project.Title,"opportunity-title"); DashboardElements.Label(info,project.ClientName+" · Budget "+DashboardElements.Money(project.Budget),"muted");
                DashboardElements.Button(opportunity,"Prepare bid",()=>Navigate("Bidding",project.Id)); opportunityRows.Add(project.Id,opportunity);
                UpdateProject(project.Id);
        }
        private VisualElement CreateProjectRow(ProjectData project)
        {
            var row=new VisualElement(); row.AddToClassList("table-row"); row.name="project-"+project.Id;
            var identity=DashboardElements.Box(row,"project-name-cell");
            DashboardElements.Button(identity,project.Title,()=>Navigate("Project Detail",project.Id),"project-link");
            DashboardElements.Label(identity,project.ClientName,"muted");
            var progress=new ProgressBar {name="progress",lowValue=0,highValue=100}; progress.AddToClassList("progress-cell"); row.Add(progress);
            DashboardElements.Label(row,"—","cell","profit"); DashboardElements.Label(row,"—","cell","deadline"); DashboardElements.Label(row,"—","cell","risk");
            row.AddManipulator(new ContextualMenuManipulator(e=>e.menu.AppendAction("Open project",_=>Navigate("Project Detail",project.Id)))); return row;
        }
        private void UpdateProject(string id)
        {
            var project=Session.Find(id); if(project==null || !projectRows.TryGetValue(id,out var row)) return;
            row.style.display=project.Status==ProjectStatus.Active || project.Status==ProjectStatus.Won || project.Status==ProjectStatus.Complete ? DisplayStyle.Flex : DisplayStyle.None;
            var progress=row.Q<ProgressBar>("progress"); progress.value=(float)Session.Progress(project); progress.title=progress.value.ToString("0")+"%";
            DashboardElements.Set(row,"profit",DashboardElements.Money(Session.ExpectedProfit(project)));
            DashboardElements.Set(row,"deadline","Hour "+project.DeadlineHour);
            string status=project.Status==ProjectStatus.Won ? "Ready to start" : Session.Risk(project);
            DashboardElements.Set(row,"risk",status); DashboardElements.Status(row.Q("risk"),status);
            opportunityRows[id].style.display=project.Status==ProjectStatus.Available ? DisplayStyle.Flex : DisplayStyle.None;
            int count=Session.Projects.Count(x=>x.Status==ProjectStatus.Active);
            Set("active-count",count.ToString()); Set("project-count-label",count+" in progress");
            root.Q("active-empty").style.display=Session.Projects.Any(x=>x.Status==ProjectStatus.Active || x.Status==ProjectStatus.Won || x.Status==ProjectStatus.Complete) ? DisplayStyle.None : DisplayStyle.Flex;
            if(screen=="Projects" || screen=="Bidding") secondary.Render(screen);
        }
        private void UpdateFinancials()
        {
            var warning=root.Q<Label>("cash-warning");
            if(warning==null) { warning=new Label {name="cash-warning"}; warning.AddToClassList("form-note"); root.Q("metrics").parent.Add(warning); }
            warning.text=Session.Finance.CashFlow.IsOverdrawn ? "CASH DEFICIT — payments exceed available cash. Review receivables and financing." : "LOW CASH — review upcoming bills and payment dates.";
            warning.style.display=Session.Finance.CashFlow.IsCashPoor ? DisplayStyle.Flex : DisplayStyle.None;
            warning.EnableInClassList("status-critical",Session.Finance.CashFlow.IsOverdrawn);
            warning.EnableInClassList("status-warning",!Session.Finance.CashFlow.IsOverdrawn);
            root.Q("cash").EnableInClassList("status-critical",Session.Finance.CashFlow.IsOverdrawn);
            Set("revenue",DashboardElements.Money(Session.Revenue)); Set("expenses",DashboardElements.Money(Session.Expenses)); Set("profit",DashboardElements.Money(Session.Profit));
            root.Q("profit").EnableInClassList("status-critical",Session.Profit<0);
            double[] values={Session.Revenue,Session.Expenses,Session.Profit,Session.CashFlow}; string[] names={"Revenue","Expenses","Profit","Cash flow"};
            double max=Math.Max(1,values.Max(x=>Math.Abs(x)));
            for(int i=0;i<names.Length;i++)
            {
                Set("chart-value-"+names[i],DashboardElements.Money(values[i])); var bar=root.Q("chart-bar-"+names[i]);
                bar.style.width=Length.Percent((float)(Math.Abs(values[i])/max*100)); bar.EnableInClassList("negative",values[i]<0); bar.EnableInClassList("positive",values[i]>=0 && i!=1);
            }
        }
        private void UpdateClock()
        {
            Set("clock",$"Day {Bootstrap.GameTime.Day} · {Bootstrap.GameTime.Hour:00}:00");
            foreach(int speed in new[]{0,1,2,5,10,25}) root.Q("speed-"+speed).EnableInClassList("selected",(int)Bootstrap.GameTime.Speed==speed);
        }
        private void AddNotification(string id)
        {
            var record=Notifications.Notifications.FirstOrDefault(x=>x.Id==id); if(record==null) return;
            var alerts=root.Q("alerts"); alerts.Insert(0,Notifications.CreateRow(record)); if(alerts.childCount>5) alerts.RemoveAt(alerts.childCount-1);
            root.Q("alerts-empty").style.display=DisplayStyle.None;
            if(screen=="Notifications") secondary.Render(screen);
        }
        public void Navigate(string target,string id=null)
        {
            if(root==null || !navButtons.ContainsKey(target)) return;
            tutorial?.Navigated(target);
            root.Q("tutorial-host").style.display=new[]{"Dashboard","Bidding","Estimates","Project Detail"}.Contains(target)?DisplayStyle.Flex:DisplayStyle.None;
            screen=target; foreach(var pair in navButtons) pair.Value.EnableInClassList("selected",pair.Key==target);
            root.Q<ScrollView>(className:"workspace-scroll").scrollOffset=Vector2.zero;
            Set("screen-title",target=="Dashboard" ? "Company overview" : target);
            root.Q("dashboard-screen").EnableInClassList("hidden",target!="Dashboard"); root.Q("project-detail-screen").EnableInClassList("hidden",target!="Project Detail");
            root.Q("secondary-screen").EnableInClassList("hidden",target=="Dashboard" || target=="Project Detail");
            if(target=="Project Detail") ProjectDetail.SelectProject(Session.Find(id)??Session.Find(ProjectDetail.SelectedProject?.Id)??Session.Projects.FirstOrDefault());
            else if(target!="Dashboard") secondary.Render(target,id);
        }
        public void ReviewTutorialEstimate(string projectId)=>tutorial?.Reviewed(projectId);
        public object Execute(SessionCommandData command)
        {
            if(Bootstrap.IsLoading)throw new InvalidOperationException("Wait for loading to finish.");
            return Session.Commands.Execute(command);
        }
        public void RunCommand(Action action)
        { try { action(); Feedback("Action completed."); } catch(Exception e) { Feedback(e.Message,true); } }
        public void Feedback(string message,bool error=false)
        { Set("feedback",message); root.Q("feedback").EnableInClassList("status-critical",error); }
        public void AskConfirmation(string message,Action action)
        { Bootstrap.GameTime.Speed=SimulationSpeed.Pause;UpdateClock();confirmAction=action; Set("confirmation-message",message); root.Q("confirmation").RemoveFromClassList("hidden"); root.Q<Button>("confirm-cancel").Focus(); }
        private void CancelConfirmation() { confirmAction=null; root.Q("confirmation").AddToClassList("hidden"); }
        private void Confirm() { var action=confirmAction; CancelConfirmation(); if(action!=null) RunCommand(action); }
        private void Set(string name,string value)=>DashboardElements.Set(root,name,value);
        private string CapturePresentation()
        {return ConstructionEmpire.Systems.Persistence.SessionJson.Serialize(new DashboardSaveData {Screen=screen,ProjectId=ProjectDetail.SelectedProject?.Id,RegionId=SelectedRegionId,Step=tutorial.Progress.Step,TutorialProject=tutorial.Progress.ProjectId,TutorialPhase=tutorial.Progress.PhaseId,GuideHidden=tutorial.Dismissed,Notifications=Notifications.Notifications.ToList()});}
        private void ValidatePresentation(string json)
        {
            if(string.IsNullOrEmpty(json))return;
            var data=ConstructionEmpire.Systems.Persistence.SessionJson.Deserialize<DashboardSaveData>(json);
            if(data==null || data.Notifications==null || data.Notifications.Count>10000)throw new InvalidOperationException("Invalid saved dashboard state.");
            var guide=new ConstructionEmpire.Data.Presentation.TutorialProgressData();guide.Restore(data.Step,data.TutorialProject,data.TutorialPhase);
        }
        private void RestorePresentation()
        {
            var data=string.IsNullOrEmpty(Bootstrap.LoadedPresentation)?new DashboardSaveData():ConstructionEmpire.Systems.Persistence.SessionJson.Deserialize<DashboardSaveData>(Bootstrap.LoadedPresentation);
            Notifications.Restore(data.Notifications);tutorial.Restore(data.Step,data.TutorialProject,data.TutorialPhase,data.GuideHidden);SelectedRegionId=data.RegionId;
            screen=Screens.Contains(data.Screen)?data.Screen:"Dashboard";ProjectDetail.SelectProject(Session.Find(data.ProjectId)??Session.Projects.FirstOrDefault());
        }
        private void OnDestroy()
        {
            menu?.Dispose();help?.Dispose();
            if(Bootstrap!=null){Bootstrap.CapturePresentation=null;Bootstrap.ValidatePresentation=null;}
            tutorial?.Dispose();
            subscriptions?.Dispose();
            if(root!=null) { root.Q<Button>("confirm-cancel").clicked-=CancelConfirmation; root.Q<Button>("confirm-submit").clicked-=Confirm; }
            confirmAction=null;if(runtimePanel!=null)Destroy(runtimePanel);
        }
    }
}
