using ConstructionEmpire.Data.Save;
using System;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Expansion;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    internal static class ExpansionScreenPresenter
    {
        public static void World(VisualElement content,MainDashboardController dashboard)
        {
            var expansion=dashboard.Session.Expansion;
            DashboardElements.Label(content,"Choose your next market","section-title");
            DashboardElements.Label(content,"Explore a country, compare its cities and open a local office. All figures use a shared GBP ledger; regional costs are fictional gameplay values.","form-note");
            var countries=expansion.Regions.GroupBy(r=>r.CountryId??"legacy").ToList();
            var countryBar=DashboardElements.Box(content,"country-grid");
            var cities=DashboardElements.Box(content,"city-grid");
            var detail=DashboardElements.Box(content,"region-detail");
            string selected=dashboard.SelectedRegionId;
            if(!expansion.Regions.Any(r=>r.Id==selected))selected=expansion.Offices.First().RegionId;
            string country=expansion.Region(selected).CountryId??"legacy";
            void Inspect(string id)
            {
                selected=id;dashboard.SelectedRegionId=id;detail.Clear();var region=expansion.Region(id);var office=expansion.Offices.FirstOrDefault(x=>x.RegionId==id);bool gate=region.MeetsThreshold(dashboard.Bootstrap.Company.Reputation,dashboard.Bootstrap.Company.CashBalance);
                DashboardElements.Label(detail,region.Name+" / "+(region.CountryName??"Home market"),"section-title");
                DashboardElements.Label(detail,region.MarketDescription??"Established regional market with a mix of local opportunities.","form-note");
                DashboardElements.Label(detail,$"Operating cost {region.CostFactor:0.00}× · Opportunity budget {region.PriceMultiplier:0.00}× · Unlock: reputation {region.ReputationThreshold:0} {(region.UnlockRule==RegionUnlockRule.ReputationOrCapital?"or":"and")} capital {DashboardElements.Money(region.CapitalThreshold)}","form-note");
                DashboardElements.Label(detail,"Common work: "+string.Join(", ",region.PreferredProjectTypes??region.ProjectTypes),"muted");
                DashboardElements.Label(detail,"Competition: "+string.Join(", ",dashboard.Session.Competitors.Where(x=>region.CompetitorIds.Contains(x.Id)).Select(x=>x.CompanyName)),"form-note");
                if(office==null)
                {
                    var actions=DashboardElements.Box(detail,"action-row");
                    var open=DashboardElements.Button(actions,"Rent office · "+DashboardElements.Money(expansion.OfficeEntryCost(region)),()=>dashboard.AskConfirmation("Establish an office in "+region.Name+"? Setup costs "+DashboardElements.Money(expansion.OfficeEntryCost(region))+" plus recurring rent. Recruit a local crew separately.",()=>{dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.OpenOffice,Id=id});dashboard.Navigate("World Map");}),"primary");open.SetEnabled(gate);
                    var buy=DashboardElements.Button(actions,"Buy office · "+DashboardElements.Money(expansion.OfficeEntryCost(region,true)),()=>dashboard.AskConfirmation("Purchase an office in "+region.Name+" for "+DashboardElements.Money(expansion.OfficeEntryCost(region,true))+"?",()=>{dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.OpenOffice,Id=id,Flag=true});dashboard.Navigate("World Map");}));buy.SetEnabled(gate);
                    if(!gate)DashboardElements.Label(detail,"Locked: build reputation or capital to meet the requirements above.","status-warning");
                }
                else
                {
                    DashboardElements.Label(detail,$"{office.Name} · {office.EmployeeIds.Count}/{office.StaffCapacity} staff · {office.ProjectCapacity} project capacity · Rent {DashboardElements.Money(office.IsOwned?0:office.RentPerDay)}/day","form-note");
                    var actions=DashboardElements.Box(detail,"action-row");
                    var hire=DashboardElements.Button(actions,"Recruit local crew · "+DashboardElements.Money(office.RegionalWorkforcePool.Count*expansion.Settings.HiringCostPerWorker),()=>dashboard.AskConfirmation("Recruit the available workforce for "+office.Name+"? Recruitment is charged now; wages accrue when working.",()=>{dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.HireCrew,Id=office.Id});dashboard.Navigate("World Map");}));hire.SetEnabled(office.RegionalWorkforcePool.Count>0);
                    var generate=DashboardElements.Button(actions,"Generate regional opportunities",()=>dashboard.RunCommand(()=>{dashboard.Execute(new SessionCommandData {Kind=SessionCommandKind.GenerateOpportunities,Id=office.Id});dashboard.Navigate("World Map");}),"primary");
                    bool room=dashboard.Session.Projects.Count(p=>p.Region==region.Name && p.Status==ProjectStatus.Available)+expansion.Settings.OpportunityBatchSize<=expansion.Settings.MaximumAvailableOpportunitiesPerRegion;generate.SetEnabled(room);if(!room)DashboardElements.Label(detail,"Review existing local opportunities before generating another batch.","form-note");
                }
                var local=dashboard.Session.Projects.Where(x=>x.SiteMap.RegionId==id).ToList();
                DashboardElements.Label(detail,"Local projects · "+local.Count,"card-title");
                if(local.Count==0)DashboardElements.Label(detail,office==null?"Open an office to discover local jobs.":"Generate regional opportunities to find new work.","empty-state");
                foreach(var project in local){var row=DashboardElements.Box(detail,"table-row");DashboardElements.Label(row,project.Title,"project-name-cell");DashboardElements.Label(row,project.Status+" · "+DashboardElements.Money(project.Budget),"cell");DashboardElements.Button(row,project.Status==ProjectStatus.Available?"Review opportunity":"Inspect project",()=>dashboard.Navigate(project.Status==ProjectStatus.Available?"Bidding":"Project Detail",project.Id));}
                foreach(var node in cities.Children())node.EnableInClassList("selected",node.name==id);
            }
            void DrawCities()
            {
                cities.Clear();var group=countries.First(g=>g.Key==country);
                foreach(var region in group){var node=DashboardElements.Button(cities,region.Name+"\n"+(expansion.Offices.Any(o=>o.RegionId==region.Id)?"OFFICE ESTABLISHED":"EXPANSION MARKET"),()=>Inspect(region.Id),"city-node");node.name=region.Id;node.tooltip=region.MarketDescription??"Select this region to inspect opportunities and expansion requirements.";}
                foreach(var node in countryBar.Children())node.EnableInClassList("selected",node.name==country);
                Inspect(group.Any(r=>r.Id==selected)?selected:group.First().Id);
            }
            foreach(var group in countries){string key=group.Key;var button=DashboardElements.Button(countryBar,(group.First().CountryName??"Home market")+"\n"+expansion.Offices.Count(o=>group.Any(r=>r.Id==o.RegionId))+" offices · "+group.Count()+" cities",()=>{country=key;DrawCities();},"country-card");button.name=key;}
            DrawCities();
        }

        public static void Site(VisualElement parent,MainDashboardController dashboard,ProjectData project)
        {
            if(project.SiteMap==null)return;dashboard.Session.Expansion.RefreshSite(project.Id);var map=project.SiteMap;
            DashboardElements.Label(parent,"Site overview · "+map.Stage,"card-title");DashboardElements.Label(parent,$"{map.Progress:0.0}% complete · {map.WorkerIds.Count} assigned workers · {map.EquipmentIds.Count} equipment/vehicles · shared material stock {map.StoredMaterialUnits:0} units","form-note");
            var boundary=DashboardElements.Box(parent,"site-boundary");boundary.style.height=190;boundary.style.position=Position.Relative;boundary.style.backgroundColor=new Color(.9f,.94f,.89f);boundary.style.borderTopWidth=2;boundary.style.borderBottomWidth=2;boundary.style.borderLeftWidth=2;boundary.style.borderRightWidth=2;
            var footprint=DashboardElements.Box(boundary,"site-footprint");footprint.style.position=Position.Absolute;footprint.style.left=Length.Percent((float)(map.FootprintX/map.BoundaryWidth*100));footprint.style.top=Length.Percent((float)(map.FootprintY/map.BoundaryHeight*100));footprint.style.width=Length.Percent((float)(map.FootprintWidth/map.BoundaryWidth*100));footprint.style.height=Length.Percent((float)(map.FootprintHeight/map.BoundaryHeight*100));footprint.style.backgroundColor=map.Progress>=100?new Color(.45f,.72f,.55f):new Color(.62f,.72f,.82f);
            DashboardElements.Label(footprint,"Building footprint\n"+map.Stage,"card-title");DashboardElements.Label(boundary,"Site boundary / office / material storage","form-note");
            var zones=DashboardElements.Box(parent,"site-zones");zones.style.flexDirection=FlexDirection.Row;zones.style.flexWrap=Wrap.Wrap;
            foreach(var zone in map.Zones){var bar=new ProgressBar {title=zone.Name,value=(float)zone.Progress};bar.style.width=Length.Percent(25);bar.style.marginBottom=8;zones.Add(bar);}
            var equipment=dashboard.Session.Workforce.Equipment.Where(x=>map.EquipmentIds.Contains(x.Id));DashboardElements.Label(parent,"Fleet: "+string.Join(", ",equipment.Select(x=>x.Name+" ("+x.Status+")")),"form-note");
        }
    }
}
