using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Expansion;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Expansion
{
    public sealed class ExpansionSystem : IRegisterableSystem,IDisposable
    {
        private readonly ManagementSession session;private readonly List<WorldMapData> regions;private readonly List<CompanyOfficeData> offices=new List<CompanyOfficeData>();
        private readonly Dictionary<string,CrewData> crews=new Dictionary<string,CrewData>();private readonly Dictionary<string,ProjectProgressEngine> engines=new Dictionary<string,ProjectProgressEngine>();
        private readonly Dictionary<string,List<ConstructionPhaseRuntimeData>> frozenTemplates;
        private readonly List<ProjectData> templates;private readonly EventSubscriptions subscriptions=new EventSubscriptions();private int nextOpportunity,sequence;private long lastTick=-1;
        public ExpansionSettings Settings {get;}public IReadOnlyList<WorldMapData> Regions=>regions.AsReadOnly();public IReadOnlyList<CompanyOfficeData> Offices=>offices.AsReadOnly();public IReadOnlyList<CrewData> Crews=>crews.Values.ToList();
        public ExpansionSystem(ManagementSession session,IEnumerable<WorldMapData> regionData,ExpansionSettings settings)
        {
            this.session=session;Settings=settings??new ExpansionSettings();Settings.Validate();regions=(regionData??SampleExpansionDataFactory.CreateStarterRegions()).Select(x=>x.Copy()).ToList();
            foreach(var r in regions)r.Validate();if(regions.Select(x=>x.Id).Distinct().Count()!=regions.Count || regions.Select(x=>x.Name).Distinct().Count()!=regions.Count)throw new ArgumentException("Region IDs and names must be unique.");
            templates=session.Projects.Select(x=>x.Copy()).ToList();frozenTemplates=templates.ToDictionary(x=>x.Id,x=>session.Phases(x.Id).Select(p=>p.Copy()).ToList());var home=regions.SingleOrDefault(x=>x.Name==session.Projects[0].Region)??throw new ArgumentException("The starting project region must exist in the world map.");home.StartingRegion=true;
            var office=NewOffice(home,true);office.EmployeeIds=session.Workforce.Workers.Select(x=>x.Id).ToList();office.RegionalWorkforcePool.Clear();office.StaffCapacity=Math.Max(office.StaffCapacity,office.EmployeeIds.Count);offices.Add(office);crews.Add(office.Id,session.Crew);engines.Add(office.Id,session.Simulation.Construction);
            foreach(var p in session.Projects){var region=regions.SingleOrDefault(x=>x.Name==p.Region)??throw new ArgumentException("Unknown project region.");p.OfficeId=region.Id==home.Id?office.Id:null;p.SiteMap=Map(p,region,region.X,region.Y);}
            subscriptions.Subscribe<PhaseProgressChangedEvent>(e=>RefreshSite(e.ProjectId));subscriptions.Subscribe<ProjectStartedEvent>(e=>RefreshSite(e.ProjectId));subscriptions.Subscribe<ProjectCompletedEvent>(e=>RefreshSite(e.ProjectId));subscriptions.Subscribe<ResourcesChangedEvent>(e=>{if(e.ProjectId==null){foreach(var p in session.Projects)RefreshSite(p.Id);}else RefreshSite(e.ProjectId);});
            subscriptions.Subscribe<MaterialDeliveredEvent>(_=>{foreach(var p in session.Projects)RefreshSite(p.Id);});
            session.Simulation.Register(this);
        }
        public WorldMapData Region(string id)=>regions.Single(x=>x.Id==id);
        public CompanyOfficeData OfficeFor(ProjectData p)=>offices.FirstOrDefault(x=>x.RegionId==p.SiteMap.RegionId);
        public bool CanBid(ProjectData p)=>OfficeFor(p)!=null;
        public IReadOnlyList<ProjectData> Opportunities(string regionId)=>session.Projects.Where(x=>x.SiteMap.RegionId==regionId && x.Status==ProjectStatus.Available && CanBid(x)).ToList();
        public IReadOnlyList<CompetitorCompanyData> CompetitorsFor(ProjectData p)
        {var r=Region(p.SiteMap.RegionId);return session.Competitors.Where(x=>r.CompetitorIds.Contains(x.Id)).Select(x=>{var c=x.Copy();c.HomeRegion=r.Name;return c;}).ToList();}
        public double OfficeEntryCost(WorldMapData region,bool purchase=false)=>(Settings.OfficeSetupCost+(purchase?Settings.OfficePurchaseCost:0))*region.CostFactor;
        public CompanyOfficeData OpenOffice(string regionId,bool purchase=false)
        {
            var r=Region(regionId);if(offices.Any(x=>x.RegionId==r.Id))throw new InvalidOperationException("An office already serves this region.");if(!r.MeetsThreshold(session.Simulation.Company.Reputation,session.Simulation.Company.CashBalance))throw new InvalidOperationException("Region reputation/capital threshold is not met.");
            double cost=OfficeEntryCost(r,purchase);Affordable(cost);var office=NewOffice(r,false);office.IsOwned=purchase;offices.Add(office);
            var engine=session.Simulation.CreateProjectEngine();engine.Conditions=session.Risks;engines.Add(office.Id,engine);session.Risks.Weather.EnsureRegion(r.Name);
            Expense(null,cost,"Establish "+office.Name);foreach(var p in session.Projects.Where(x=>x.SiteMap.RegionId==r.Id)){p.OfficeId=office.Id;p.SiteMap.OfficeId=office.Id;}
            GameEvents.Publish(new ExpansionChangedEvent(r.Id));return office;
        }
        private CompanyOfficeData NewOffice(WorldMapData r,bool home)
        {
            var result=new CompanyOfficeData {Id="office-"+r.Id,Name=home?session.Settings.OfficeName:r.Name+" Office",RegionId=r.Id,Location=r.Name,EstablishmentCost=Settings.OfficeSetupCost*r.CostFactor,RentPerDay=home?0:Settings.OfficeRentPerDay*r.CostFactor,PurchaseCost=Settings.OfficePurchaseCost*r.CostFactor,OperationalEfficiency=Settings.OfficeEfficiency,ProjectCapacity=Settings.OfficeProjectCapacity,StaffCapacity=Settings.OfficeStaffCapacity};
            var roster=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment();foreach(var w in roster.Workers){w.Id=r.Id+"-candidate-"+w.Id;w.Name=r.Name+" / "+w.Name;w.CurrentProjectId=null;w.CurrentTaskId=null;w.WagePerHour*=r.CostFactor;result.RegionalWorkforcePool.Add(w);}result.Validate();return result;
        }
        public CrewData HireRegionalCrew(string officeId)
        {
            var office=offices.Single(x=>x.Id==officeId);if(crews.ContainsKey(office.Id))throw new InvalidOperationException("This office already has its starter crew.");var candidates=office.RegionalWorkforcePool.ToList();if(candidates.Count==0 || office.EmployeeIds.Count+candidates.Count>office.StaffCapacity)throw new InvalidOperationException("Regional pool or staff capacity is insufficient.");Affordable(candidates.Count*Settings.HiringCostPerWorker);
            var crew=new CrewData {Id=office.Id+"-crew",Name=office.Location+" Crew",Members=candidates};crew.Validate();session.Workforce.Workers.AddRange(candidates);office.EmployeeIds.AddRange(candidates.Select(x=>x.Id));office.RegionalWorkforcePool.Clear();crews.Add(office.Id,crew);if(session.Development.Settings.StartWithRegularShifts)session.Development.SetWorkPolicy(crew.Id,false,false);Expense(null,candidates.Count*Settings.HiringCostPerWorker,"Recruit "+crew.Name);GameEvents.Publish(new ResourcesChangedEvent(null));GameEvents.Publish(new ExpansionChangedEvent(office.RegionId));return crew;
        }
        public CrewData CrewFor(ProjectData p)
        {var office=OfficeFor(p);if(office==null || !crews.TryGetValue(office.Id,out var crew))throw new InvalidOperationException("Open an office and recruit a regional crew first.");return crew;}
        public IReadOnlyList<CrewData> CrewsFor(ProjectData p){var office=OfficeFor(p);return office!=null && crews.TryGetValue(office.Id,out var crew)?new[]{crew}:Array.Empty<CrewData>();}
        public ProjectProgressEngine EngineFor(ProjectData p)
        {var office=OfficeFor(p)??throw new InvalidOperationException("Open an office in the project region first.");return engines[office.Id];}
        public void ValidateStart(ProjectData p)
        {var office=OfficeFor(p)??throw new InvalidOperationException("This region needs an office.");if(session.Projects.Count(x=>x.OfficeId==office.Id && x.Status==ProjectStatus.Active)>=office.ProjectCapacity)throw new InvalidOperationException("Office active-project capacity reached.");p.OfficeId=office.Id;p.SiteMap.OfficeId=office.Id;}
        public IReadOnlyList<ProjectData> GenerateOpportunities(string officeId,int count=0)
        {
            var office=offices.Single(x=>x.Id==officeId);var r=Region(office.RegionId);var candidates=templates.Where(x=>r.ProjectTypes.Contains(x.BuildingType)).ToList();if(candidates.Count==0)throw new InvalidOperationException("No configured template supports this region's project types.");
            count=count==0?Settings.OpportunityBatchSize:count;if(count<1 || count>Settings.MaximumAvailableOpportunitiesPerRegion)throw new ArgumentOutOfRangeException(nameof(count));if(session.Projects.Count(x=>x.Region==r.Name && x.Status==ProjectStatus.Available)+count>Settings.MaximumAvailableOpportunitiesPerRegion)throw new InvalidOperationException("Review existing regional opportunities before generating more.");
            var added=new List<ProjectData>();for(int i=0;i<count;i++)
            {
                if(Settings.ProceduralOpportunities)
                {
                    var pool=Settings.OpportunityProfiles.Where(x=>x.MinimumLevel<=session.Development.Level && candidates.Any(t=>t.BuildingType==x.BuildingType)).ToList();
                    if(pool.Count==0)throw new InvalidOperationException("No unlocked work profiles in this region.");
                    if(r.PreferredProjectTypes!=null){var preferred=pool.Where(x=>r.PreferredProjectTypes.Contains(x.BuildingType)).ToList();for(int weight=1;weight<Math.Min(10,r.PreferredCategoryWeight);weight++)pool.AddRange(preferred);}
                    var profile=pool[session.Simulation.Random.Next(pool.Count)];var basis=candidates.First(x=>x.BuildingType==profile.BuildingType);
                    var draft=OpportunityGenerator.Create(basis,frozenTemplates[basis.Id],profile,session.Simulation.Random,session.Simulation.Time.TotalHours,r.PriceMultiplier/(string.IsNullOrEmpty(r.CountryId)?1:regions.First(x=>x.StartingRegion).PriceMultiplier));
                    var generated=draft.Project;generated.Id="regional-"+r.Id+"-"+(++nextOpportunity);generated.Title+=" / "+r.Name+" "+nextOpportunity;generated.Region=r.Name;generated.Location=r.Name+" site "+nextOpportunity;generated.OfficeId=office.Id;
                    generated.SiteMap=Map(generated,r,r.X+5+session.Simulation.Random.Next(15),r.Y+5+session.Simulation.Random.Next(15));
                    generated.SiteMap.Zones=draft.Phases.Select(x=>new SiteZoneData {PhaseId=x.Id,Name=x.Name}).ToList();
                    session.AddOpportunity(generated,draft.Phases);added.Add(session.Find(generated.Id));continue;
                }
                var template=candidates[session.Simulation.Random.Next(candidates.Count)];var p=template.Copy();p.Id="regional-"+r.Id+"-"+(++nextOpportunity);p.Region=r.Name;p.Location=r.Name+" site "+nextOpportunity;p.Title=template.Title+" / "+r.Name+" "+nextOpportunity;p.OfficeId=office.Id;p.Status=ProjectStatus.Available;p.Schedule=null;p.SubmittedBidId=null;p.SubmittedBidAmount=0;p.ContractValue=0;
                double scale=1+(session.Simulation.Random.NextDouble()*2-1)*Settings.RegionalPriceVariation;p.Size*=(float)scale;p.Budget*=(float)(scale*r.PriceMultiplier);p.EstimatedDurationHours*=(float)scale;p.DeadlineHour=checked(session.Simulation.Time.TotalHours+(long)Math.Ceiling(template.DeadlineHour*scale));
                p.Requirements.LaborHours*=(float)scale;p.Requirements.MaterialUnits*=(float)scale;p.Requirements.EquipmentHours*=(float)scale;p.Requirements.SubcontractorHours*=(float)scale;
                p.SiteMap=Map(p,r,r.X+5+session.Simulation.Random.Next(15),r.Y+5+session.Simulation.Random.Next(15));var phases=session.Phases(template.Id).Select(x=>x.Copy()).ToList();foreach(var phase in phases){phase.Progress=0;phase.AccruedCost=0;phase.ReworkHoursRemaining=0;phase.DurationHours*=scale;phase.LaborHours*=scale;phase.Cost*=scale;foreach(var material in phase.MaterialRequirements)material.Quantity*=scale;}
                session.AddOpportunity(p,phases);added.Add(session.Find(p.Id));
            }
            GameEvents.Publish(new OpportunitiesChangedEvent(r.Id));return added;
        }
        public double OfficeDistance(ProjectData p){var office=OfficeFor(p);if(office==null)return 0;return Region(office.RegionId).DistanceTo(p.SiteMap.WorldX,p.SiteMap.WorldY);}
        public int TravelHours(ProjectData p)=>(int)Math.Ceiling(OfficeDistance(p)/Settings.WorkerTravelKilometresPerHour);
        private double SupplyDistance(ProjectData p)=>!string.IsNullOrEmpty(Region(p.SiteMap.RegionId).CountryId) && OfficeFor(p)!=null?OfficeDistance(p):regions.First(x=>x.StartingRegion).DistanceTo(p.SiteMap.WorldX,p.SiteMap.WorldY);
        public int DeliveryHours(ProjectData p)=>(int)Math.Ceiling(SupplyDistance(p)/Settings.DeliveryKilometresPerHour);
        public double DeliveryCost(ProjectData p)=>SupplyDistance(p)*Settings.TransportCostPerKilometre;
        public void ChargeTravel(ProjectData p,int machineCount)=>Expense(p.Id,OfficeDistance(p)*Settings.TransportCostPerKilometre*(1+machineCount),"Crew/equipment travel to "+p.Location);
        public double Efficiency(ProjectData p)=>OfficeFor(p)?.OperationalEfficiency??1;
        private void Affordable(double cost){if(session.Finance.CashFlow.Cash<(decimal)cost)throw new InvalidOperationException("Insufficient liquid cash for this expansion purchase.");}
        private void Expense(string p,double cost,string reason){if(cost>0)session.Finance.AccrueExpense(p,(decimal)cost,FinancialCategory.Office,"expansion-"+(++sequence),reason,session.Simulation.Time.TotalHours);}
        private SiteMapData Map(ProjectData p,WorldMapData region,double x,double y)=>new SiteMapData {ProjectId=p.Id,RegionId=region.Id,OfficeId=p.OfficeId,WorldX=x,WorldY=y,Zones=session.Phases(p.TemplateId==null?p.Id:templates.FirstOrDefault(t=>t.TemplateId==p.TemplateId)?.Id??p.Id).Select(t=>new SiteZoneData {PhaseId=t.Id,Name=t.Name}).ToList()};
        public void RefreshSite(string id)
        {
            var p=session.Find(id);if(p?.SiteMap==null)return;var map=p.SiteMap;var phases=session.Phases(id);map.Progress=session.Progress(p);foreach(var zone in map.Zones)zone.Progress=phases.Single(x=>x.Id==zone.PhaseId).Progress;
            map.Stage=p.Status==ProjectStatus.Complete?"Finished building":phases.FirstOrDefault(x=>!x.IsComplete)?.Name??"Planned";map.WorkerIds=session.Workforce.Workers.Where(x=>x.CurrentProjectId==id).Select(x=>x.Id).ToList();map.EquipmentIds=session.Workforce.Equipment.Where(x=>x.CurrentProjectId==id).Select(x=>x.Id).ToList();map.StoredMaterialUnits=session.Inventory.Stock.Sum(x=>x.Quantity);
        }
        public void OnTick(TickContext context)
        {
            if(context.TotalHours<=lastTick)return;lastTick=context.TotalHours;if(!context.StartsDay)return;
            foreach(var office in offices.Where(x=>!x.IsOwned && x.RentPerDay>0))Expense(null,office.RentPerDay,"Rent: "+office.Name);
            foreach(var p in session.Projects.Where(x=>x.Status==ProjectStatus.Active))Expense(p.Id,OfficeDistance(p)*Settings.RemoteOverheadPerKilometrePerDay,"Distance overhead: "+p.Title);
        }
        public void Dispose(){subscriptions.Dispose();session.Simulation.Unregister(this);foreach(var e in engines.Values.Where(x=>!ReferenceEquals(x,session.Simulation.Construction)))session.Simulation.Unregister(e);}
    }
}
