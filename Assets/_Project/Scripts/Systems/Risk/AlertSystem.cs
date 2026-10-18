using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Data.Bidding;
namespace ConstructionEmpire.Systems.Risk
{
    /// <summary>Current, resolving threshold warnings, separate from the immutable reactive notification history.</summary>
    public sealed class AlertSystem : IDisposable
    {
        private readonly Dictionary<string,ProactiveAlert> active=new Dictionary<string,ProactiveAlert>();private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private readonly Func<IEnumerable<ConstructionEmpire.Data.Procurement.ProcurementOrder>> orders;
        private readonly RiskSettings settings;private readonly Func<string,ProjectData> project;private readonly Func<IEnumerable<EquipmentRuntimeData>> equipment;private readonly Func<IEnumerable<EmployeeData>> workers;private readonly Func<IEnumerable<FinancialObligation>> obligations;
        public IReadOnlyList<ProactiveAlert> Active=>active.Values.OrderByDescending(x=>x.Critical).ThenBy(x=>x.Id,StringComparer.Ordinal).ToList();
        public AlertSystem(RiskSettings settings,Func<string,ProjectData> project,Func<IEnumerable<EquipmentRuntimeData>> equipment,Func<IEnumerable<EmployeeData>> workers,Func<IEnumerable<FinancialObligation>> obligations,Func<IEnumerable<ConstructionEmpire.Data.Procurement.ProcurementOrder>> orders=null)
        {this.orders=orders;this.settings=settings;this.project=project;this.equipment=equipment;this.workers=workers;this.obligations=obligations;subscriptions.Subscribe<ForecastChangedEvent>(e=>Evaluate(e.Forecast));}
        public void Evaluate(ProjectForecast f)
        {
            var p=project(f.ProjectId);if(p==null)return;var previous=active.Values.Where(x=>x.ProjectId==p.Id).Select(x=>x.Id+"|"+x.Description).OrderBy(x=>x).ToArray();
            foreach(var key in active.Where(x=>x.Value.ProjectId==p.Id).Select(x=>x.Key).ToArray())active.Remove(key);
            if(p.Status==ProjectStatus.Active)
            {
                if(f.DelayHours>=settings.ScheduleWarningHours)Add(f,"schedule","Completion is slipping",$"Forecast is {f.DelayHours/24:0.0} days beyond the contract deadline.",p.Id,"Schedule");
                if(f.ExpectedFinalCost>=f.Budget*settings.BudgetWarningRatio)Add(f,"budget","Budget exposure",$"Expected final cost {f.ExpectedFinalCost:0}; budget {f.Budget:0}. Review the remaining work before spending.",p.Id,"Finance",f.ExpectedFinalCost>f.Budget);
                foreach(var machine in equipment().Where(x=>x.CurrentProjectId==p.Id && x.Condition<=settings.EquipmentConditionWarning))Add(f,"equipment-"+machine.Id,"Equipment condition is low",$"{machine.Name}: {machine.Condition:P0} condition. Schedule maintenance.",machine.Id,"Equipment");
                foreach(var worker in workers().Where(x=>x.CurrentProjectId==p.Id && x.Fatigue>=settings.FatigueWarning))Add(f,"fatigue-"+worker.Id,"Worker fatigue is high",$"{worker.Name}: {worker.Fatigue:P0} fatigue. Release the crew for rest.",worker.Id,"Employees");
                if(p.Schedule!=null && p.Schedule.Phases.Any(x=>x.IsCriticalPath && !x.ActualCompletion.HasValue && x.TotalFloat<=settings.CriticalFloatWarningHours) && f.Risk.Level>=RiskLevel.High)Add(f,"critical-path","Critical-path work is at risk","Little schedule flexibility remains while project risk is high.",p.Id,"Schedule");
                if(orders!=null)foreach(var order in orders().Where(x=>x.ProjectId==p.Id && x.Status==ConstructionEmpire.Data.Procurement.ProcurementStatus.Delayed))Add(f,"delivery-"+order.Id,"Material supply at risk",order.MaterialId+" is delayed until hour "+order.DueHour+"; protect dependent work before stock runs out.",order.Id,"Materials");
                foreach(var bill in obligations().Where(x=>x.ProjectId==p.Id && x.Receivable && !x.Paid && x.DueHour<f.UpdatedHour))Add(f,"payment-"+bill.Id,"Client payment overdue",$"{bill.Description}: due at hour {bill.DueHour}.",bill.Id,"Finance");
            }
            var current=active.Values.Where(x=>x.ProjectId==p.Id).Select(x=>x.Id+"|"+x.Description).OrderBy(x=>x).ToArray();if(!previous.SequenceEqual(current))GameEvents.Publish(new ProactiveAlertsChangedEvent(p.Id));
        }
        private void Add(ProjectForecast f,string key,string title,string description,string entity,string screen,bool critical=false)
        {string id=f.ProjectId+":"+key;active[id]=new ProactiveAlert {Id=id,ProjectId=f.ProjectId,EntityId=entity,Title=title,Description=description,Screen=screen,Hour=f.UpdatedHour,Critical=critical};}
        public void Dispose()=>subscriptions.Dispose();
    }
}
