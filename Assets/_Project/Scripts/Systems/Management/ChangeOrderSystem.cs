using System;
using System.Linq;
using System.Collections.Generic;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Events;
namespace ConstructionEmpire.Systems.Management
{
    public sealed class ChangeOrderSystem
    {
        private readonly ManagementSession session;private readonly List<ChangeOrderData> orders=new List<ChangeOrderData>();
        public IReadOnlyList<ChangeOrderData> Orders=>orders.AsReadOnly();
        public ChangeOrderSystem(ManagementSession session){this.session=session;}
        public ChangeOrderData Propose(string projectId)
        {
            var p=Active(projectId);if(orders.Any(x=>x.ProjectId==p.Id))throw new InvalidOperationException("This project's scope proposal has already been reviewed.");
            var phase=session.Phases(p.Id).LastOrDefault(x=>x.Progress==0 && !x.RequiresSubcontractor && !session.Scheduling.Bookings.Any(b=>b.Open && b.Data.ProjectId==p.Id && b.Data.PhaseId==x.Id))??throw new InvalidOperationException("No unstarted, unbooked internal phase can accept a scope change.");
            var settings=session.Development.Settings;double fraction=settings.ChangeOrderScopeFraction;
            double cost=(phase.Cost+phase.LaborHours*session.Crew.CostPerHour/Math.Max(1,session.Crew.Members.Count)+phase.MaterialRequirements.Sum(m=>m.Quantity*session.Materials.Single(x=>x.Id==m.MaterialId).Price))*fraction;
            var order=new ChangeOrderData {Id="change-"+p.Id,ProjectId=p.Id,PhaseId=phase.Id,Description="Client requests additional scope in "+phase.Name,ScopeFraction=fraction,EstimatedCost=cost,OfferedPrice=cost*(1+settings.ChangeOrderMarkup),ExtensionHours=(long)Math.Ceiling(phase.DurationHours*fraction)};
            orders.Add(order);Changed(p.Id);return order;
        }
        public void Negotiate(string id)
        {
            var o=Pending(id);if(o.Negotiated)throw new InvalidOperationException("The client has already considered a counteroffer.");o.Negotiated=true;
            double chance=Math.Min(1,session.Development.Settings.NegotiationBaseAcceptance+session.Development.Client(Active(o.ProjectId)).Relationship);
            if(session.Simulation.Random.NextDouble()<chance)o.OfferedPrice*=1+session.Development.Settings.NegotiationIncrease;else o.Status=ChangeOrderStatus.Rejected;
            Changed(o.ProjectId);
        }
        public void Decide(string id,bool accept)
        {
            var o=Pending(id);var p=Active(o.ProjectId);
            if(!accept){o.Status=ChangeOrderStatus.Rejected;Changed(p.Id);return;}
            var phase=session.Phases(p.Id).Single(x=>x.Id==o.PhaseId);
            if(phase.Progress>0 || session.Scheduling.Bookings.Any(x=>x.Open && x.Data.ProjectId==p.Id && x.Data.PhaseId==phase.Id))throw new InvalidOperationException("Scope can only change before the phase is booked or started.");
            var contract=session.Finance.GetContract(p.Id)??throw new InvalidOperationException("A signed contract is required.");
            long amendedDeadline=checked(p.DeadlineHour+o.ExtensionHours);
            phase.DurationHours*=1+o.ScopeFraction;phase.LaborHours*=1+o.ScopeFraction;phase.Cost*=1+o.ScopeFraction;
            foreach(var m in phase.MaterialRequirements)m.Quantity*=1+o.ScopeFraction;
            p.ContractValue+=(float)o.OfferedPrice;p.Budget+=(float)o.EstimatedCost;p.DeadlineHour=amendedDeadline;
            o.Status=ChangeOrderStatus.Accepted;session.Finance.AmendScope(p.Id,(decimal)o.OfferedPrice,(decimal)o.EstimatedCost,p.DeadlineHour);
            session.Scheduling.Update(session.Simulation.Time.TotalHours,new[]{p.Id});session.Risks.Forecasts.Refresh(p.Id);Changed(p.Id);
        }
        private ProjectData Active(string id){var p=session.Find(id);if(p==null || p.Status!=ProjectStatus.Active)throw new InvalidOperationException("Select an active project.");return p;}
        private ChangeOrderData Pending(string id){var o=orders.SingleOrDefault(x=>x.Id==id);if(o==null || o.Status!=ChangeOrderStatus.Proposed)throw new InvalidOperationException("No open scope proposal.");return o;}
        private void Changed(string id){GameEvents.Publish(new ResourcesChangedEvent(id));GameEvents.Publish(new CompanyDevelopmentChangedEvent(session.Development,"Contract scope updated"));}
    }
}
