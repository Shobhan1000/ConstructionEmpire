using ConstructionEmpire.Data.Subcontractors;
using ConstructionEmpire.Systems.Subcontractors;
using System;
using ConstructionEmpire.Data.Procurement;
using ConstructionEmpire.Systems.Procurement;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Bidding;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Simulation;
using ConstructionEmpire.Systems.Finance;
using ConstructionEmpire.Data.Finance;
namespace ConstructionEmpire.Systems.Management
{
    /// <summary>Owns the playable management slice. UI calls commands; simulation owns all state changes.</summary>
    public sealed class ManagementSession : IRegisterableSystem, IDisposable
    {
        private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private readonly Dictionary<string,List<ConstructionPhaseRuntimeData>> phases;
        private readonly HashSet<string> budgetAlerts=new HashSet<string>();
        private readonly ScheduledEventQueue scheduler;
        private readonly List<ProjectData> projects;
        
        private readonly List<MaterialCatalogEntry> materials;
        private int nextBid;
        private bool disposed;
        public ConstructionEmpire.Systems.Market.MarketSession MarketSimulation {get;}
        public ConstructionEmpire.Systems.Market.MarketConditionsSystem Market=>MarketSimulation?.Conditions;
        public ConstructionEmpire.Systems.Market.CompetitorAIController CompetitorAI=>MarketSimulation?.Competitors;
        public ConstructionEmpire.Systems.Risk.ProjectRiskSystem Risks {get;}
        public ConstructionEmpire.Systems.Expansion.ExpansionSystem Expansion {get;}
        public ProjectProgressEngine EngineFor(string id)=>Expansion.EngineFor(Find(id)??throw new ArgumentException("Unknown project."));
        public ConstructionEmpire.Systems.Research.ResearchSession Research {get;}
        public ConstructionEmpire.Systems.Persistence.SessionCommandJournal Commands {get;}
        public CompanyDevelopmentSystem Development {get;}
        public ChangeOrderSystem ChangeOrders {get;}
        public SimulationManager Simulation { get; }
        public ManagementSettings Settings { get; }
        public IReadOnlyList<ProjectData> Projects => projects.AsReadOnly();
        public IReadOnlyList<ProcurementOrder> Orders => Procurement.Orders;
        public SupplierSystem Suppliers {get;}
        public SubcontractorSystem Subcontractors {get;}
        public ConstructionEmpire.Systems.Scheduling.SchedulingSystem Scheduling {get;}
        public MaterialProcurementSystem Procurement {get;}
        public IReadOnlyList<MaterialCatalogEntry> Materials => materials.AsReadOnly();
        public StarterWorkforce Workforce { get; }
        public CrewData Crew { get; }
        public MaterialInventoryData Inventory { get; } = new MaterialInventoryData();
        public EstimationSystem Estimation { get; }
        public BiddingSystem Bidding { get; }
        public IReadOnlyList<CompetitorCompanyData> Competitors { get; }
        public FinanceSystem Finance { get; }
        public double Revenue => (double)Finance.Revenue;
        public double ProcurementCost { get; private set; }
        public double Expenses => (double)Finance.Expenses;
        public double Profit => Revenue-Expenses;
        public double OpeningCash { get; }
        public double CashFlow => (double)Finance.CashFlow.NetCashFlow;
        public double UnpaidConstructionCost => (double)Finance.AccountsPayable;
        public ManagementSession(SimulationManager simulation, IEnumerable<ProjectData> opportunities, StarterWorkforce workforce,
            Dictionary<string,List<ConstructionPhaseRuntimeData>> phaseTemplates,EstimationRates rates,BiddingConfiguration bidding,
            IReadOnlyList<CompetitorCompanyData> competitors,ManagementSettings settings,FinanceSettings financeSettings=null,IEnumerable<SupplierData> supplierProfiles=null,ProcurementSettings procurementSettings=null,IEnumerable<SubcontractorCompanyData> subcontractors=null,SubcontractorSettings subcontractorSettings=null,ConstructionEmpire.Data.Risk.RiskSettings riskSettings=null,IEnumerable<ConstructionEmpire.Data.Expansion.WorldMapData> regions=null,ConstructionEmpire.Data.Expansion.ExpansionSettings expansionSettings=null,ConstructionEmpire.Data.Market.MarketSettings marketSettings=null,IEnumerable<ConstructionEmpire.Data.Research.ResearchUpgradeData> researchTree=null,ConstructionEmpire.Data.Research.ResearchSettings researchSettings=null,DevelopmentSettings developmentSettings=null)
        {
            Commands=new ConstructionEmpire.Systems.Persistence.SessionCommandJournal(this);
            Simulation=simulation ?? throw new ArgumentNullException(nameof(simulation)); Settings=settings ?? throw new ArgumentNullException(nameof(settings)); settings.Validate();
            projects=opportunities.Select(x=>x.Copy()).ToList(); if(projects.Select(x=>x.Id).Distinct().Count()!=projects.Count) throw new ArgumentException("Duplicate project ID.");
            foreach(var project in projects) project.Validate();
            Workforce=workforce ?? throw new ArgumentNullException(nameof(workforce));
            Crew=new CrewData { Id="starter-crew",Name="Renovation Crew",Members=new List<EmployeeData>(workforce.Workers) }; Crew.Validate();
            foreach(var equipment in workforce.Equipment) equipment.Validate();
            phases=new Dictionary<string,List<ConstructionPhaseRuntimeData>>();
            foreach(var project in projects) { phases.Add(project.Id,phaseTemplates[project.Id].Select(x=>x.Copy()).ToList()); new PhaseDependencyGraph(phases[project.Id]); }
            materials=settings.Materials.Select(x=>x.Copy()).ToList();
            foreach(var material in phases.Values.SelectMany(x=>x).SelectMany(x=>x.MaterialRequirements))
                if(!materials.Any(x=>x.Id==material.MaterialId)) throw new ArgumentException("Missing material catalog entry: "+material.MaterialId);
            Estimation=new EstimationSystem(rates); Bidding=new BiddingSystem(bidding,simulation.Random); Competitors=competitors.Select(x=>x.Copy()).ToList();
            OpeningCash=simulation.Company.CashBalance;
            Scheduling=new ConstructionEmpire.Systems.Scheduling.SchedulingSystem();simulation.Construction.EnableScheduling(Scheduling);
            Finance=new FinanceSystem(simulation.Company,simulation.Time,financeSettings??new FinanceSettings()); simulation.Register(Finance);
            scheduler=new ScheduledEventQueue(simulation.Time.TotalHours); simulation.Register(scheduler); simulation.Register(this);
            var procurementConfig=procurementSettings??new ProcurementSettings();
            Suppliers=new SupplierSystem(supplierProfiles??SampleSupplierDataFactory.CreateStarterSuppliers(),simulation.Random,procurementConfig);simulation.Register(Suppliers);
            Procurement=new MaterialProcurementSystem(Suppliers,Inventory,Finance,scheduler,simulation.Time,simulation.Random,procurementConfig);
            Subcontractors=new SubcontractorSystem(subcontractors??SampleSubcontractorDataFactory.CreateStarterSubcontractors(),simulation.Construction,Finance,simulation.Time,simulation.Random,subcontractorSettings??new SubcontractorSettings(),id=>EngineFor(id));
            var subcontractRate=(subcontractorSettings??new SubcontractorSettings()).BaseHourlyRate;
            Estimation.DetailedCosts=project=>new ProjectCostDetails {
                SubcontractorCost=phases[project.Id].Where(x=>x.RequiresSubcontractor).Sum(phase=>phase.LaborHours*subcontractRate*Subcontractors.Companies.Where(c=>c.Specialty==phase.SubcontractorSpecialty).Select(c=>c.QuotePricingTendency).DefaultIfEmpty(1).Average()),
                FixedPhaseCost=phases[project.Id].Sum(x=>x.Cost)};
            Risks=new ConstructionEmpire.Systems.Risk.ProjectRiskSystem(this,riskSettings);
            Expansion=new ConstructionEmpire.Systems.Expansion.ExpansionSystem(this,regions,expansionSettings);Risks.Initialize();MarketSimulation=new ConstructionEmpire.Systems.Market.MarketSession(this,marketSettings??new ConstructionEmpire.Data.Market.MarketSettings());
            Research=new ConstructionEmpire.Systems.Research.ResearchSession(this,researchTree,researchSettings??new ConstructionEmpire.Data.Research.ResearchSettings());
            Development=new CompanyDevelopmentSystem(this,developmentSettings);ChangeOrders=new ChangeOrderSystem(this);
            subscriptions.Subscribe<PhaseProgressChangedEvent>(OnProgress);
            subscriptions.Subscribe<ProjectCompletedEvent>(OnCompleted);
            subscriptions.Subscribe<BidWonEvent>(e=> { var project=Find(e.ProjectId); if(project!=null) EnsureContract(project); });
        }
        public void AddOpportunity(ProjectData project,IEnumerable<ConstructionPhaseRuntimeData> tasks)
        {EnsureLive();if(project==null || Find(project.Id)!=null)throw new ArgumentException("Project ID must be unique.");project.Validate();var copy=project.Copy();var list=tasks.Select(x=>x.Copy()).ToList();new PhaseDependencyGraph(list);phases.Add(copy.Id,list);projects.Add(copy);Development?.Refresh();Risks.Weather.EnsureRegion(copy.Region);Risks.Forecasts.Refresh(copy.Id);}
        public ConstructionEmpire.Systems.Scheduling.ScheduledAssignment ReserveCrew(string id,string phase,CrewData crew,IEnumerable<EquipmentRuntimeData> equipment,long start,long end)
        {var p=Find(id)??throw new ArgumentException("Unknown project.");if(!Expansion.CrewsFor(p).Contains(crew))throw new InvalidOperationException("Use this office's regional workforce.");if(start<Simulation.Time.TotalHours+Expansion.TravelHours(p))throw new InvalidOperationException("Allow "+Expansion.TravelHours(p)+" travel hours before the reservation starts.");var machines=equipment.ToList();var booking=EngineFor(id).ScheduleCrew(id,phase,crew,machines,start,end);Expansion.ChargeTravel(p,machines.Count);return booking;}
        public ProcurementQuote QuoteMaterial(string id,string supplier,string material,double quantity,ShippingMethod shipping)=>Procurement.Quote(supplier,material,quantity,shipping,Expansion.DeliveryHours(Find(id)),Expansion.DeliveryCost(Find(id)));
        public ProjectData Find(string id) => projects.Find(x=>x.Id==id);
        public IReadOnlyList<ConstructionPhaseRuntimeData> Phases(string projectId) => phases[projectId].AsReadOnly();
        public double Progress(ProjectData project) => phases[project.Id].Sum(x=>x.Progress*x.DurationHours)/phases[project.Id].Sum(x=>x.DurationHours);
        public double Cost(ProjectData project) => (double)Finance.ProjectCosts(project.Id);
        public double ForecastCost(ProjectData project) => Risks?.Forecasts.Get(project.Id)!=null ? Risks.Forecasts.Get(project.Id).ExpectedFinalCost : Finance.GetContract(project.Id)!=null ? (double)Finance.Forecast(project.Id).ForecastCost : Estimation.Estimate(project,0,0).EstimatedCost;
        public double ExpectedProfit(ProjectData project) => (Finance.GetContract(project.Id)!=null?(double)(Finance.Forecast(project.Id).ForecastProfit+Finance.Forecast(project.Id).ForecastCost)-ForecastCost(project):project.ContractValue-ForecastCost(project));
        public string Risk(ProjectData project) => project.Status==ProjectStatus.Complete ? "Complete" : (Risks?.Forecasts.Get(project.Id)?.Risk.Level.ToString()??"Low")+" Risk";
        public BidOutcome SubmitBid(string projectId,float amount)
        {
            EnsureLive(); var project=Find(projectId) ?? throw new ArgumentException("Unknown project.");
            if(!Expansion.CanBid(project))throw new InvalidOperationException("Open an office in this region before bidding.");
            var configured=Settings.Capabilities;
            var capabilities=new BidderCapabilities { Reputation=Simulation.Company.Reputation,Experience=configured.Experience,Quality=configured.Quality,
                ClientRelationship=(float)Development.Client(project).Relationship,Certifications=configured.Certifications,MarketFit=configured.MarketFit };
            return Bidding.SubmitBid(project,"player-bid-"+(++nextBid),amount,project.EstimatedDurationHours,capabilities,Expansion.CompetitorsFor(project),Simulation.Time.TotalHours);
        }
        private void EnsureContract(ProjectData project) => Finance.RegisterContract(project,(decimal)Estimation.Estimate(project,0,0).EstimatedCost);
        public void StartProject(string id)
        { EnsureLive(); var project=Find(id); if(project==null || project.Status!=ProjectStatus.Won) throw new InvalidOperationException("A won project is required.");
          Expansion.ValidateStart(project);EnsureContract(project); EngineFor(id).StartProject(project,phases[id],Inventory,Simulation.Time.TotalHours); Finance.StartContract(id); }
        public void AssignCrew(string projectId,string phaseId)
        {
            EnsureLive(); var phase=phases[projectId].Single(x=>x.Id==phaseId);
            var machines=new List<EquipmentRuntimeData>();
            foreach(var requirement in phase.EquipmentRequirements)
            {
                var available=Workforce.Equipment.Where(e=>e.EquipmentType==requirement.Type && e.Status==EquipmentStatus.Available).Take(requirement.Count).ToList();
                if(available.Count<requirement.Count) throw new InvalidOperationException("Required equipment is unavailable."); machines.AddRange(available);
            }
            var crew=Expansion.CrewFor(Find(projectId));int travel=Expansion.TravelHours(Find(projectId));
            if(travel==0)EngineFor(projectId).AssignCrew(projectId,phaseId,crew,machines);else EngineFor(projectId).ScheduleCrew(projectId,phaseId,crew,machines,Simulation.Time.TotalHours+travel,long.MaxValue);Expansion.ChargeTravel(Find(projectId),machines.Count);
            GameEvents.Publish(new ResourcesChangedEvent(projectId));
        }
        public IReadOnlyList<SubcontractorQuote> RequestSubcontractorQuotes(string projectId,string phaseId)
        { EnsureLive();if(Find(projectId)==null)throw new ArgumentException("Unknown project.");return Subcontractors.RequestQuotes(projectId,phases[projectId].Single(x=>x.Id==phaseId)); }
        public void AwardSubcontractor(string quoteId,string projectId,string phaseId)
        {
            EnsureLive();var phase=phases[projectId].Single(x=>x.Id==phaseId);
            if(!Subcontractors.GetQuotes(projectId,phaseId).Any(x=>x.Id==quoteId))throw new ArgumentException("Quote does not belong to this phase.");
            var machines=new List<EquipmentRuntimeData>();
            foreach(var requirement in phase.EquipmentRequirements)
            {
                var available=Workforce.Equipment.Where(e=>e.EquipmentType==requirement.Type && e.Status==EquipmentStatus.Available).Take(requirement.Count).ToList();
                if(available.Count<requirement.Count)throw new InvalidOperationException("Required equipment is unavailable.");machines.AddRange(available);
            }
            Subcontractors.Award(quoteId,machines);GameEvents.Publish(new ResourcesChangedEvent(projectId));
        }
        public void ReleaseCrew(CrewData selectedCrew=null)
        {
            EnsureLive(); var crew=selectedCrew??Crew;string id=crew.CurrentProjectId,task=crew.CurrentTaskId; if(id==null) return;
            EngineFor(id).ReleaseCrew(id,task); GameEvents.Publish(new ResourcesChangedEvent(id));
        }
        public ProcurementOrder OrderMaterial(string projectId,string materialId,double quantity,string supplierId=null,ShippingMethod shipping=ShippingMethod.Standard)
        {
            EnsureLive(); if(Find(projectId)==null) throw new ArgumentException("Choose a project.");
            // Default remains deterministic; UI exposes every supplier and shipping option.
            supplierId=supplierId??Suppliers.ForMaterial(materialId).OrderByDescending(x=>x.Reliability).ThenBy(x=>x.Id,StringComparer.Ordinal).First().Id;
            var order=Procurement.PlaceOrder(projectId,supplierId,materialId,quantity,shipping,Expansion.DeliveryHours(Find(projectId)),Expansion.DeliveryCost(Find(projectId)));ProcurementCost+=order.Cost;return order;
        }
        public void Refuel(string equipmentId)
        {
            EnsureLive(); var equipment=Workforce.Equipment.Single(x=>x.Id==equipmentId);
            double cost=(equipment.FuelCapacity-equipment.CurrentFuel)*Settings.FuelPricePerLitre;
            if(cost<=0) return;
            if(cost>float.MaxValue) throw new InvalidOperationException("Refuel cost exceeds cash range.");
            Finance.AccrueExpense(equipment.CurrentProjectId,(decimal)cost,FinancialCategory.Transportation,"fuel-"+(Finance.Ledger.Count+1),"Refuel: "+equipment.Name,Simulation.Time.TotalHours);
            ProcurementCost+=cost; equipment.CurrentFuel=equipment.FuelCapacity; GameEvents.Publish(new ResourcesChangedEvent(equipment.CurrentProjectId));
        }
        private void OnProgress(PhaseProgressChangedEvent e)
        {
            var project=Find(e.ProjectId); if(project==null) return;
            Finance.UpdateProgress(project.Id,Progress(project));
            double cost=Cost(project);
            if(cost>project.Budget && budgetAlerts.Add(project.Id)) GameEvents.Publish(new BudgetExceededEvent(project.Id,project.Budget,(float)cost));
        }
        private void OnCompleted(ProjectCompletedEvent e)
        {
            var project=Find(e.ProjectId); if(project==null || project.Status!=ProjectStatus.Complete) return;
            // Inspection outcomes now feed the final quality rating.
            Finance.CompleteContract(project.Id,project.Site.Quality);
        }
        public void OnTick(TickContext context) { /* Proactive deadline warnings are owned by AlertSystem. */ }
        private void EnsureLive() { if(disposed) throw new ObjectDisposedException(nameof(ManagementSession)); }
        public void Dispose() { if(disposed) return; subscriptions.Dispose(); Development.Dispose(); Research.Dispose(); MarketSimulation.Dispose(); Expansion.Dispose(); Risks.Dispose(); Subcontractors.Dispose(); Procurement.Dispose(); Simulation.Unregister(Suppliers); Finance.Dispose(); Simulation.Unregister(Finance); Simulation.Unregister(scheduler); Simulation.Unregister(this); disposed=true; }
    }
}
