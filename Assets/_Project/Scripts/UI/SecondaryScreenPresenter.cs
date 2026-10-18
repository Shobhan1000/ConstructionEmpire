using ConstructionEmpire.Data.Save;
using System;
using ConstructionEmpire.Data.Procurement;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    /// <summary>Read models and command controls for the Milestone 4 navigation shell.</summary>
    internal sealed class SecondaryScreenPresenter
    {
        private readonly MainDashboardController dashboard;
        private readonly VisualElement content;
        private string selectedId;
        private string projectQuery="",projectStatus="All statuses",projectRegion="All regions",projectSort="Status";
        public SecondaryScreenPresenter(MainDashboardController dashboard,VisualElement content) { this.dashboard=dashboard; this.content=content; }
        public void RefreshForProject(string screen,string projectId)
        { if(screen=="Finance" || screen=="Employees" || screen=="Equipment" || screen=="Crews" || screen=="Materials") Render(screen); }
        public void RefreshResources(string screen)
        { if(screen=="Materials" || screen=="Equipment" || screen=="Employees" || screen=="Crews" || screen=="Finance") Render(screen); }
        public void Render(string screen,string id=null)
        {
            if(id!=null) selectedId=id;
            content.Clear(); var session=dashboard.Session;
            switch(screen)
            {
                case "Research": ResearchScreenPresenter.Build(content,dashboard);break;
                case "World Map": ExpansionScreenPresenter.World(content,dashboard);break;
                case "Schedule":
                    var scheduledProject=session.Find(selectedId)??session.Projects.First();
                    ProjectChooser(scheduledProject,p=> {selectedId=p.Id;Render("Schedule");}); ScheduleScreenPresenter.Build(content,dashboard,scheduledProject);break;
                case "Projects":
                    ProjectBrowser(); break;
                case "Bidding": case "Estimates": BidForm(); break;
                case "Employees":
                    foreach(var employee in session.Workforce.Workers)
                    {
                        var row=DashboardElements.Box(content,"table-row"); DashboardElements.Label(row,employee.Name+" · "+employee.Age,"project-name-cell");
                        DashboardElements.Label(row,employee.WorkerType.ToString(),"cell"); DashboardElements.Label(row,$"Skill {employee.Skill:P0} · {employee.Experience:0} yrs","cell");
                        DashboardElements.Label(row,$"Morale {employee.Morale:P0} · Fatigue {employee.Fatigue:P0}","cell");
                        DashboardElements.Label(row,DashboardElements.Money(employee.WagePerHour)+" / h","cell"); DashboardElements.Label(row,employee.AbsentUntilHour>session.Simulation.Time.TotalHours?"Absent until hour "+employee.AbsentUntilHour:employee.CurrentTaskId??"Available","cell");
                        row.tooltip="Certifications: "+string.Join(", ",employee.Certifications)+$" · Reliability {employee.Reliability:P0} · Safety {employee.Safety:P0}";
                    } break;
                case "Crews":
                    foreach(var crew in session.Expansion.Crews){DashboardElements.Label(content,crew.Name,"card-title");DashboardElements.Label(content,$"{crew.Members.Count} members · {DashboardElements.Money(crew.CostPerHour)}/h · {crew.CurrentTaskId??"Unassigned"}","form-note");DashboardElements.Button(content,"Release crew",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.ReleaseCrew,Id=crew.Id})));DevelopmentScreenPresenter.CrewPolicy(content,dashboard,crew);}
                    DashboardElements.Button(content,"Manage regional offices",()=>dashboard.Navigate("World Map"));break;
                case "Equipment": Equipment(content,dashboard); break;
                case "Materials":
                    var materialProject=session.Find(selectedId)??session.Projects.First();
                    ProjectChooser(materialProject,p=> { selectedId=p.Id; Render("Materials"); }); Materials(content,dashboard,materialProject); break;
                case "Subcontractors":
                    var subProject=session.Find(selectedId)??session.Projects.First();
                    ProjectChooser(subProject,p=> {selectedId=p.Id;Render("Subcontractors");}); SubcontractorScreenPresenter.Build(content,dashboard,subProject); break;
                case "Notifications":
                    foreach(var record in dashboard.Notifications.Notifications.Reverse()) content.Add(dashboard.Notifications.CreateRow(record));
                    if(content.childCount==0) DashboardElements.Label(content,"No notifications yet.","empty-state"); break;
                case "Suppliers":
                    foreach(var supplier in session.Suppliers.Suppliers)
                    {
                        DashboardElements.Label(content,supplier.Name,"card-title");
                        DashboardElements.Label(content,supplier.Personality+$" · Reliability {supplier.Reliability:P0} · Relationship {supplier.Relationship:P0}","form-note");
                        foreach(var offer in supplier.Offers) DashboardElements.Label(content,$"{offer.MaterialId} · {DashboardElements.Money(offer.CurrentPrice)} / unit · {offer.LeadHours} h · Quality {offer.Quality:P0} · Capacity {offer.Capacity-offer.Reserved:0}","table-row");
                    } break;
                case "Finance": FinanceScreenPresenter.Build(content,dashboard); break;
                case "Company":
                    DevelopmentScreenPresenter.Company(content,dashboard);MarketScreenPresenter.Build(content,dashboard);
                    var name=new TextField("Company name") { value=dashboard.Bootstrap.Company.CompanyName }; name.AddToClassList("form-field"); content.Add(name);
                    DashboardElements.Button(content,"Save company name",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.RenameCompany,Text=name.value})),"primary");
                    DashboardElements.Label(content,session.Settings.OfficeName+" · "+session.Settings.OfficeLocation,"form-note"); break;
                case "Settings":DevelopmentScreenPresenter.Settings(content,dashboard);break;
                case "Clients":DevelopmentScreenPresenter.Clients(content,dashboard);break;
                default: DashboardElements.Label(content,screen+" is not implemented yet. Navigation and layout are ready for its dedicated milestone.","empty-state"); break;
            }
        }
        public void RefreshSchedule(string projectId){var p=dashboard.Session.Find(selectedId)??dashboard.Session.Projects.First();if(p.Id==projectId)ScheduleScreenPresenter.Refresh(content,dashboard,p);}
        public void RefreshFinance()=>FinanceScreenPresenter.Refresh(content,dashboard);
        private void ProjectBrowser()
        {
            var session=dashboard.Session;
            var filters=DashboardElements.Box(content,"browser-filters");
            var search=new TextField("Search") {value=projectQuery};filters.Add(search);search.tooltip="Search project title, client or location.";
            var statuses=new[]{"All statuses"}.Concat(Enum.GetNames(typeof(ProjectStatus))).ToList();
            var status=new DropdownField("Status",statuses,Math.Max(0,statuses.IndexOf(projectStatus)));filters.Add(status);
            var regions=new[]{"All regions"}.Concat(session.Projects.Select(p=>p.Region).Distinct().OrderBy(x=>x)).ToList();
            var region=new DropdownField("Region",regions,Math.Max(0,regions.IndexOf(projectRegion)));filters.Add(region);
            var sorts=new[]{"Status","Deadline","Budget","Progress"}.ToList();var sort=new DropdownField("Sort by",sorts,Math.Max(0,sorts.IndexOf(projectSort)));filters.Add(sort);
            var count=DashboardElements.Label(content,"","muted");var rows=DashboardElements.Box(content,"project-browser");
            void Draw()
            {
                rows.Clear();var projects=session.Projects.Where(p=>(status.index==0 || p.Status.ToString()==status.value) && (region.index==0 || p.Region==region.value) && (p.Title+" "+p.ClientName+" "+p.Location).IndexOf(search.value??"",StringComparison.OrdinalIgnoreCase)>=0);
                projects=sort.value=="Deadline"?projects.OrderBy(p=>p.DeadlineHour):sort.value=="Budget"?projects.OrderByDescending(p=>p.Budget):sort.value=="Progress"?projects.OrderByDescending(p=>session.Progress(p)):projects.OrderBy(p=>p.Status==ProjectStatus.Active?0:p.Status==ProjectStatus.Won?1:p.Status==ProjectStatus.Available?2:3).ThenBy(p=>p.Title);
                var list=projects.ToList();count.text=list.Count+" of "+session.Projects.Count+" projects";
                foreach(var project in list)
                {
                    var row=DashboardElements.Box(rows,"project-browser-row");var info=DashboardElements.Box(row,"project-browser-info");
                    DashboardElements.Label(info,project.Title,"card-title");DashboardElements.Label(info,project.ClientName+" · "+project.Location,"muted");
                    DashboardElements.Label(info,$"{project.Status} · {session.Progress(project):0}% · Budget {DashboardElements.Money(project.Budget)} · Due {RiskScreenPresenter.Date(project.DeadlineHour)}","form-note");
                    var next=ConstructionEmpire.Systems.Management.ProjectReadiness.Get(session,project);if(project.Status==ProjectStatus.Active || project.Status==ProjectStatus.Won)DashboardElements.Label(info,next.Title+" — "+next.Description,"muted");
                    var actions=DashboardElements.Box(row,"browser-actions");DashboardElements.Button(actions,project.Status==ProjectStatus.Available?"Review bid":"Open project",()=>dashboard.Navigate(project.Status==ProjectStatus.Available?"Bidding":"Project Detail",project.Id),"primary");
                    DashboardElements.Button(actions,"Schedule",()=>dashboard.Navigate("Schedule",project.Id));
                }
                if(list.Count==0)DashboardElements.Label(rows,"No matching projects. Clear a filter or explore another region.","empty-state");
            }
            search.RegisterValueChangedCallback(_=>{projectQuery=search.value;Draw();});status.RegisterValueChangedCallback(_=>{projectStatus=status.value;Draw();});region.RegisterValueChangedCallback(_=>{projectRegion=region.value;Draw();});sort.RegisterValueChangedCallback(_=>{projectSort=sort.value;Draw();});Draw();
        }
        private void ProjectChooser(ProjectData project,Action<ProjectData> changed)
        {
            var projects=dashboard.Session.Projects.ToList();
            var field=new DropdownField("Project",projects.Select(x=>x.Title).ToList(),projects.FindIndex(x=>x.Id==project.Id)); field.AddToClassList("form-field"); content.Add(field);
            field.RegisterValueChangedCallback(e=>changed(projects[field.index]));
        }
        private void BidForm()
        {
            var session=dashboard.Session; var project=session.Find(selectedId)??session.Projects.FirstOrDefault(x=>x.Status==ProjectStatus.Available)??session.Projects.First(); selectedId=project.Id;
            var regions=session.Expansion.Offices.Select(o=>session.Expansion.Region(o.RegionId)).ToList();
            var regionChoice=new DropdownField("Office region",regions.Select(x=>x.Name).ToList(),Math.Max(0,regions.FindIndex(x=>x.Name==project.Region)));content.Add(regionChoice);
            var local=session.Projects.Where(x=>x.Region==regionChoice.value).ToList();if(!local.Contains(project)){project=local.FirstOrDefault();if(project==null){DashboardElements.Label(content,"Generate opportunities from World Map for this office.","empty-state");return;}selectedId=project.Id;}
            var localChoice=new DropdownField("Regional project",local.Select(x=>x.Title).ToList(),local.IndexOf(project));content.Add(localChoice);
            regionChoice.RegisterValueChangedCallback(_=>{var next=session.Projects.FirstOrDefault(x=>x.Region==regionChoice.value);if(next!=null){selectedId=next.Id;Render("Bidding");}else dashboard.Navigate("World Map");});
            localChoice.RegisterValueChangedCallback(_=>{selectedId=local[localChoice.index].Id;Render("Bidding");});
            DashboardElements.Label(content,$"{project.ClientName} · {project.Location} · {project.BuildingType} · {project.Size:0} units · {project.QualityRequirement} quality","form-note");
            DashboardElements.Label(content,project.Site.Description,"form-note").tooltip="The generated client brief describes this opportunity’s work emphasis, delivery window and site conditions. Press F1 for help.";
            DashboardElements.Label(content,$"Scope: {session.Phases(project.Id).Count} phases · {session.Phases(project.Id).Sum(p=>p.LaborHours):0} planned labor hours","muted");
            DashboardElements.Label(content,$"Budget {DashboardElements.Money(project.Budget)} · duration {project.EstimatedDurationHours:0} hours · deadline hour {project.DeadlineHour} · status {project.Status}","form-note");
            var rates=dashboard.Bootstrap.ManagementConfiguration.Bidding;
            var profit=new FloatField("Desired profit (%)") {value=rates.DefaultProfitFraction*100}; profit.AddToClassList("form-field"); content.Add(profit);
            var risk=new FloatField("Risk contingency (%)") {value=rates.DefaultRiskFraction*100}; risk.AddToClassList("form-field"); content.Add(risk);
            risk.tooltip=DashboardTooltips.For("estimate-contingency");
            var lines=new VisualElement(); content.Add(lines);
            var bid=new FloatField("Your bid (GBP)"); bid.AddToClassList("form-field"); content.Add(bid);
            void Estimate()
            {
                try
                {
                    var estimate=session.Estimation.Estimate(project,profit.value/100d,risk.value/100d); lines.Clear();
                    foreach(var pair in new[]{("Labor",estimate.Labor),("Materials",estimate.Materials),("Equipment",estimate.Equipment),("Subcontractors",estimate.Subcontractors),("Overhead",estimate.Overhead),("Desired profit",estimate.DesiredProfit),("Risk margin",estimate.RiskMargin)})
                        DashboardElements.Label(lines,pair.Item1+"  "+DashboardElements.Money(pair.Item2),"table-row");
                    DashboardElements.Label(lines,$"Estimate confidence {estimate.Accuracy:P0} · Expected cost range {DashboardElements.Money(estimate.LowerExpectedCost)}–{DashboardElements.Money(estimate.UpperExpectedCost)}","form-note");
                    bid.SetValueWithoutNotify((float)estimate.BidPrice); dashboard.Feedback("Estimate updated. You may override your bid to compete below cost.");
                }
                catch(Exception e) { dashboard.Feedback(e.Message,true); }
            }
            profit.RegisterValueChangedCallback(_=>Estimate()); risk.RegisterValueChangedCallback(_=>Estimate()); Estimate();
            var review=DashboardElements.Button(content,"Review estimate",()=>dashboard.ReviewTutorialEstimate(project.Id));review.SetEnabled(project.Status==ProjectStatus.Available);
            var submit=DashboardElements.Button(content,"Review and submit bid",()=>dashboard.AskConfirmation("Submit "+DashboardElements.Money(bid.value)+" for "+project.Title+"? This opportunity can be bid on once. The award is not guaranteed.",()=>
            {
                var result=(ConstructionEmpire.Systems.Bidding.BidOutcome)dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.SubmitBid,Id=project.Id,Amount=bid.value});
                dashboard.Navigate(result.Won ? "Project Detail" : "Bidding",project.Id);
            }),"primary"); submit.SetEnabled(project.Status==ProjectStatus.Available && session.Expansion.CanBid(project));
            DashboardElements.Label(content,"Competition signals","card-title");
            if(!string.IsNullOrEmpty(project.AwardedCompetitorName))DashboardElements.Label(content,"Lost to "+project.AwardedCompetitorName,"form-note");
            foreach(var competitor in session.Expansion.CompetitorsFor(project)) DashboardElements.Label(content,competitor.CompanyName+" · "+session.Bidding.GetQualitativeSignal(competitor),"table-row");
            DashboardElements.Label(content,"Competitor bid amounts are private. Award uses price, reputation, experience, quality, client relationship, duration, certifications and market fit.","form-note");
        }
        public static void Equipment(VisualElement content,MainDashboardController dashboard,string projectId=null)
        {
            if(projectId==null)DevelopmentScreenPresenter.EquipmentCatalog(content,dashboard);
            foreach(var equipment in dashboard.Session.Workforce.Equipment.Where(x=>x.Status!=ConstructionEmpire.Data.Construction.EquipmentStatus.Retired && (projectId==null || x.CurrentProjectId==projectId)))
            {
                var row=DashboardElements.Box(content,"table-row"); DashboardElements.Label(row,equipment.Name,"project-name-cell");
                DashboardElements.Label(row,equipment.Status+" · "+equipment.Location,"cell"); DashboardElements.Label(row,"Condition "+equipment.Condition.ToString("P0"),"cell");
                DashboardElements.Label(row,$"Fuel {equipment.CurrentFuel:0.0} / {equipment.FuelCapacity:0} L","cell");
                var button=DashboardElements.Button(row,"Refuel",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.Refuel,Id=equipment.Id}))); button.SetEnabled(equipment.CurrentFuel<equipment.FuelCapacity);
                button.tooltip="Cost: "+DashboardElements.Money((equipment.FuelCapacity-equipment.CurrentFuel)*dashboard.Session.Settings.FuelPricePerLitre);
                var repair=DashboardElements.Button(row,"Repair / maintain",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.Repair,Id=equipment.Id})));repair.SetEnabled(equipment.Status==ConstructionEmpire.Data.Construction.EquipmentStatus.Broken || equipment.Condition<dashboard.Session.Risks.Settings.RepairCondition);repair.tooltip=DashboardElements.Money(equipment.MaintenanceCost);
                if(projectId==null)DashboardElements.Button(row,equipment.IsRented?"Return rental":"Sell",()=>dashboard.AskConfirmation("Remove "+equipment.Name+" from your fleet? All bookings must be released first.",()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.ReleaseEquipment,Id=equipment.Id})));
                row.tooltip=$"Operating {DashboardElements.Money(equipment.CostPerHour)}/h · Purchase {DashboardElements.Money(equipment.PurchasePrice)} · Rental {DashboardElements.Money(equipment.RentalPricePerDay)}/day · Maintenance {DashboardElements.Money(equipment.MaintenanceCost)}";
            }
            if(projectId!=null && !dashboard.Session.Workforce.Equipment.Any(x=>x.CurrentProjectId==projectId)) DashboardElements.Label(content,"No equipment assigned. Equipment is assigned with eligible phase crews.","empty-state");
        }
        private static void SupplyPlan(VisualElement content,MainDashboardController dashboard,ProjectData project)
        {
            var panel=DashboardElements.Box(content,"supply-plan");DashboardElements.Label(panel,"Plan remaining supplies","card-title");
            DashboardElements.Label(panel,"Subtracts shared stock and all incoming company orders from this project's remaining requirements. Stock is shared, not reserved: other projects may use it. Review before purchasing.","form-note");
            var policy=new DropdownField("Supplier priority",new System.Collections.Generic.List<string>{"Reliability","Lowest cost","Fastest delivery"},0);panel.Add(policy);
            var lines=DashboardElements.Box(panel,"supply-lines");
            void Preview()
            {
                lines.Clear();try {
                    var preference=(ConstructionEmpire.Systems.Management.SupplyPreference)policy.index;
                    var plan=ConstructionEmpire.Systems.Management.SupplyPlanner.Preview(dashboard.Session,project.Id,preference);
                    var breakdown=new Foldout {text="Itemised supply plan · "+plan.Lines.Count+" materials",value=false};lines.Add(breakdown);
                    foreach(var line in plan.Lines)DashboardElements.Label(breakdown,$"{line.MaterialId} · {line.Quantity:0.##} units · {line.SupplierName} · {line.LeadHours} h · {line.Reliability:P0} reliability · {DashboardElements.Money(line.Cost)}","form-note");
                    foreach(var problem in plan.Problems)DashboardElements.Label(lines,problem,"status-warning");
                    DashboardElements.Label(lines,plan.Lines.Count==0 && plan.Problems.Count==0?"Current stock and incoming orders cover the remaining scope.":"Estimated total · "+DashboardElements.Money(plan.Total),"card-title");
                    var buy=DashboardElements.Button(lines,"Review supply order",()=>dashboard.AskConfirmation("Order "+plan.Lines.Count+" material lines for up to "+DashboardElements.Money(plan.Total)+"? Cash is charged immediately. Delivery reliability still applies. The plan is checked again before purchase.",()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.OrderSupplyPlan,Id=project.Id,Start=policy.index,Amount=plan.Total})),"primary");buy.SetEnabled(plan.Lines.Count>0 && plan.Problems.Count==0);
                }catch(Exception e){DashboardElements.Label(lines,e.Message,"status-warning");}
            }
            policy.RegisterValueChangedCallback(_=>Preview());Preview();
        }
        public static void Materials(VisualElement content,MainDashboardController dashboard,ProjectData project)
        {
            var session=dashboard.Session;
            SupplyPlan(content,dashboard,project);
            DashboardElements.Label(content,"Shared company inventory. Orders are charged now and delivered after the listed lead time.","form-note");
            foreach(var required in session.Phases(project.Id).SelectMany(x=>x.MaterialRequirements.Select(m=>new {m.MaterialId,Quantity=m.Quantity*(1-x.Progress/100)})).GroupBy(x=>x.MaterialId))
            {
                var material=session.Materials.Single(x=>x.Id==required.Key); double stock=session.Inventory.GetQuantity(material.Id);
                double pending=session.Orders.Where(x=>!x.Delivered && x.MaterialId==material.Id).Sum(x=>x.Quantity);
                var row=DashboardElements.Box(content,"table-row"); DashboardElements.Label(row,material.Name,"project-name-cell");
                DashboardElements.Label(row,$"Stock {stock:0.##} · need {required.Sum(x=>x.Quantity):0.##} · pending {pending:0.##}","cell");
                var suppliers=session.Suppliers.ForMaterial(material.Id).OrderByDescending(x=>x.Reliability).ToList();
                var choice=new DropdownField(suppliers.Select(x=>x.Name).ToList(),0);choice.style.width=180;row.Add(choice);
                var quantity=new DoubleField {value=Math.Max(0,Math.Round(required.Sum(x=>x.Quantity)-stock-pending,6))};quantity.style.width=70;row.Add(quantity);
                var expedited=new Toggle("Express");row.Add(expedited);
                var quote=DashboardElements.Label(row,"Enter quantity","cell");
                Button orderButton=null;
                Action refresh=()=> {if(quantity.value<=0 || double.IsNaN(quantity.value) || double.IsInfinity(quantity.value)) {quote.text="Enter a positive quantity";orderButton?.SetEnabled(false);return;}try {orderButton?.SetEnabled(true);var q=session.QuoteMaterial(project.Id,suppliers[choice.index].Id,material.Id,quantity.value,expedited.value ? ShippingMethod.Expedited : ShippingMethod.Standard);quote.text=DashboardElements.Money(q.Total)+$" · {q.LeadHours} h · Discount {q.Discount:P0}";}catch(Exception e){quote.text=e.Message;orderButton?.SetEnabled(false);}};
                choice.RegisterValueChangedCallback(_=>refresh());quantity.RegisterValueChangedCallback(_=>refresh());expedited.RegisterValueChangedCallback(_=>refresh());refresh();
                orderButton=DashboardElements.Button(row,"Order",()=>dashboard.RunCommand(()=>dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.OrderMaterial,Id=project.Id,OtherId=material.Id,Amount=quantity.value,ThirdId=suppliers[choice.index].Id,Flag=expedited.value})));
                refresh();
                row.style.flexWrap=Wrap.Wrap;row.tooltip=material.StorageRequirement;

            }
            DashboardElements.Label(content,"Pending deliveries","card-title");
            foreach(var order in session.Orders.Where(x=>x.ProjectId==project.Id && !x.Delivered)) DashboardElements.Label(content,$"{order.MaterialId}: {order.Quantity:0.##} · {order.Status} · due hour {order.DueHour}","table-row");
        }
    }
}

