using System;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Research;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Data.Procurement;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Research
{
    /// <summary>Session-owned integration. Automation uses the same procurement and scheduling commands as the UI.</summary>
    public sealed class ResearchSession : IRegisterableSystem,IDisposable
    {
        private readonly ManagementSession session;private readonly ResearchSettings settings;private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private long lastHour=-1,spendDay=-1;private double spent;private bool disposed;
        public ResearchSystem System {get;}
        public bool ProcurementEnabled {get;private set;}public bool SchedulingEnabled {get;private set;}
        public string AutomationStatus {get;private set;}="Automation is off.";
        public ResearchSession(ManagementSession session,System.Collections.Generic.IEnumerable<ResearchUpgradeData> tree,ResearchSettings settings)
        {
            this.session=session;this.settings=settings;settings.Validate();
            System=new ResearchSystem(tree??DefaultResearchTreeFactory.CreateStarterTree(),()=>session.Finance.CashFlow.Cash,(cost,id)=>session.Finance.AccrueExpense(null,cost,FinancialCategory.Other,"research-"+id,"Research: "+id,session.Simulation.Time.TotalHours),Apply,settings.CompanyLevel);
            session.Simulation.RegisterBefore(this,session.Simulation.Construction);
            subscriptions.Subscribe<ConstructionEmpire.Data.Expansion.ExpansionChangedEvent>(_=>Apply());
            subscriptions.Subscribe<FinanceChangedEvent>(e=>{if(ReferenceEquals(e.Company,session.Simulation.Company))System.RefreshAvailability();});Apply();
        }
        public void Apply()
        {
            session.Estimation.BaseAccuracy=settings.BaseEstimationAccuracy;session.Estimation.ResearchAccuracyBonus=System.Modifier(ResearchEffectKind.EstimationAccuracy);
            foreach(var crew in session.Expansion.Crews)crew.ResearchEfficiencyMultiplier=1+System.Modifier(ResearchEffectKind.CrewEfficiency);
            foreach(var equipment in session.Workforce.Equipment){equipment.FuelConsumptionMultiplier=1-Math.Min(.9,System.Modifier(ResearchEffectKind.FuelSavings));equipment.BreakdownRateMultiplier=1-Math.Min(.9,System.Modifier(ResearchEffectKind.EquipmentReliability));}
            session.Procurement.ResearchDiscountFraction=Math.Min(.9,System.Modifier(ResearchEffectKind.ProcurementDiscount));
        }
        public void SetAutomation(bool procurement,bool scheduling)
        {
            if(procurement && System.Modifier(ResearchEffectKind.ReorderThreshold)<=0 || scheduling && System.Modifier(ResearchEffectKind.AutoScheduling)<=0)throw new InvalidOperationException("Research this automation first.");
            ProcurementEnabled=procurement;SchedulingEnabled=scheduling;AutomationStatus="Preferences updated. Next check runs on the configured simulation cadence.";GameEvents.Publish(new ResearchChangedEvent(System,null,"Automation changed"));
        }
        public void OnTick(TickContext tick)
        {
            if(disposed || tick.TotalHours<=lastHour)return;lastHour=tick.TotalHours;System.OnTick(tick);
            if(!ProcurementEnabled && !SchedulingEnabled || tick.TotalHours%settings.AutomationEveryHours!=0)return;
            if(spendDay!=tick.Day){spendDay=tick.Day;spent=0;}
            string status="No eligible work or material shortfall.";
            if(ProcurementEnabled)status=Reorder();
            if(SchedulingEnabled)
                foreach(var p in session.Projects.Where(x=>x.Status==ProjectStatus.Active).OrderBy(x=>x.DeadlineHour).ThenBy(x=>x.Id,StringComparer.Ordinal))
                {
                    var crew=session.Expansion.CrewsFor(p).FirstOrDefault();if(crew==null || crew.CurrentProjectId!=null || session.Scheduling.Bookings.Any(x=>x.Open && x.Source.Id==crew.Id))continue;
                    foreach(var phase in new PhaseDependencyGraph(session.Phases(p.Id)).EligiblePhases().Where(x=>!x.RequiresSubcontractor && !session.Scheduling.Bookings.Any(b=>b.Open && b.Data.ProjectId==p.Id && b.Data.PhaseId==x.Id)))
                    {
                        try{session.AssignCrew(p.Id,phase.Id);status="Assigned "+crew.Name+" to "+phase.Name;break;}
                        catch(InvalidOperationException e){status="Scheduling waiting: "+e.Message;}
                    }
                }
            if(status!=AutomationStatus){AutomationStatus=status;GameEvents.Publish(new ResearchChangedEvent(System,null,"Automation changed"));}
        }
        private string Reorder()
        {
            var active=session.Projects.Where(x=>x.Status==ProjectStatus.Active).OrderBy(x=>x.DeadlineHour).ThenBy(x=>x.Id,StringComparer.Ordinal).ToList();
            var requirements=active.SelectMany(p=>session.Phases(p.Id).SelectMany(phase=>phase.MaterialRequirements.Select(m=>new {Project=p,Material=m.MaterialId,Remaining=m.Quantity*(1-phase.Progress/100)}))).Where(x=>x.Remaining>0).GroupBy(x=>x.Material);
            string status="Inventory and incoming orders cover reorder thresholds.";
            foreach(var group in requirements)
            {
                double required=group.Sum(x=>x.Remaining),position=session.Inventory.GetQuantity(group.Key)+session.Orders.Where(x=>x.MaterialId==group.Key && !x.Delivered).Sum(x=>x.Quantity);
                if(position>=required*Math.Min(1,System.Modifier(ResearchEffectKind.ReorderThreshold)))continue;
                var p=group.First().Project;double allowance=Math.Min(settings.DailyAutomationBudget-spent,(double)session.Finance.CashFlow.Cash-settings.CashReserve);
                if(allowance<=0){status="Reordering waiting: cash reserve or daily budget reached.";continue;}
                bool ordered=false;
                foreach(var supplier in session.Suppliers.ForMaterial(group.Key).OrderBy(x=>session.Suppliers.Offer(x.Id,group.Key).CurrentPrice).ThenBy(x=>x.Id,StringComparer.Ordinal))
                {
                    var offer=session.Suppliers.Offer(supplier.Id,group.Key);double quantity=Math.Min(required-position,offer.Capacity-offer.Reserved);
                    // Conservative cap uses undiscounted price, then confirms through the shared quote formula.
                    quantity=Math.Min(quantity,(allowance-session.Expansion.DeliveryCost(p))/offer.CurrentPrice);if(quantity<=.0001)continue;
                    var quote=session.QuoteMaterial(p.Id,supplier.Id,group.Key,quantity,ShippingMethod.Standard);if(quote.Total>allowance || quote.Total<=0)continue;
                    var order=session.OrderMaterial(p.Id,group.Key,quantity,supplier.Id);spent+=order.Cost;status="Ordered "+quantity.ToString("0.##")+" units of "+group.Key+"; today spent "+spent.ToString("0.00");ordered=true;break;
                }
                if(!ordered)status="Reordering waiting: insufficient supplier capacity or budget.";
            }
            return status;
        }
        public void Dispose(){if(disposed)return;disposed=true;subscriptions.Dispose();session.Simulation.Unregister(this);}
    }
}
