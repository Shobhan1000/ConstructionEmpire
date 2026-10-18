using System;
using System.Linq;
using System.Collections.Generic;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Procurement;
using ConstructionEmpire.Systems.Management;
namespace ConstructionEmpire.Systems.Persistence
{
    public sealed class SessionCommandJournal
    {
        private readonly ManagementSession session;
        private readonly List<SessionCommandData> commands=new List<SessionCommandData>();
        private SessionStartData initial;private bool executing;private string invalidReason;
        public void MarkDiagnosticMutation(string reason){invalidReason=reason;}
        public SessionCommandJournal(ManagementSession session){this.session=session;}
        public void Initialize(SessionStartData start){if(initial!=null)throw new InvalidOperationException("Session origin is immutable.");initial=SessionJson.Copy(start);}
        public object Execute(SessionCommandData command)
        {
            if(command==null || executing)throw new InvalidOperationException("A player command cannot be nested.");
            if(!Enum.IsDefined(typeof(SessionCommandKind),command.Kind) || command.EquipmentIds==null || double.IsNaN(command.Amount) || double.IsInfinity(command.Amount) || double.IsNaN(command.Value) || double.IsInfinity(command.Value))throw new ArgumentException("Invalid player command.");
            var copy=SessionJson.Copy(command);copy.Hour=session.Simulation.Time.TotalHours;copy.Failed=false;executing=true;
            try{return Dispatch(copy);}
            catch{copy.Failed=true;throw;}
            finally{commands.Add(copy);executing=false;}
        }
        internal void Replay(SessionCommandData command)
        {
            bool failed=false;
            try{Dispatch(command);}catch{failed=true;if(!command.Failed)throw;}
            if(failed!=command.Failed)throw new InvalidOperationException("Save command outcome differs from the recorded session.");
            commands.Add(SessionJson.Copy(command));
        }
        private object Dispatch(SessionCommandData c)
        {
            var s=session;
            switch(c.Kind)
            {
                case SessionCommandKind.SubmitBid:return s.SubmitBid(c.Id,(float)c.Amount);
                case SessionCommandKind.StartProject:s.StartProject(c.Id);break;
                case SessionCommandKind.AssignCrew:s.AssignCrew(c.Id,c.OtherId);break;
                case SessionCommandKind.ReserveCrew:return s.ReserveCrew(c.Id,c.OtherId,s.Expansion.Crews.Single(x=>x.Id==c.ThirdId),c.EquipmentIds.Select(id=>s.Workforce.Equipment.Single(x=>x.Id==id)),c.Start,c.End);
                case SessionCommandKind.CancelBooking:s.EngineFor(c.Id).CancelBooking(c.OtherId);break;
                case SessionCommandKind.RequestQuotes:return s.RequestSubcontractorQuotes(c.Id,c.OtherId);
                case SessionCommandKind.AwardSubcontractor:s.AwardSubcontractor(c.ThirdId,c.Id,c.OtherId);break;
                case SessionCommandKind.ReleaseCrew:s.ReleaseCrew(s.Expansion.Crews.Single(x=>x.Id==c.Id));break;
                case SessionCommandKind.OrderMaterial:return s.OrderMaterial(c.Id,c.OtherId,c.Amount,c.ThirdId,c.Flag?ShippingMethod.Expedited:ShippingMethod.Standard);
                case SessionCommandKind.Refuel:s.Refuel(c.Id);break;
                case SessionCommandKind.Repair:s.Risks.Events.Repair(s.Workforce.Equipment.Single(x=>x.Id==c.Id));break;
                case SessionCommandKind.TrainCrew:s.Risks.Events.TrainCrew(s.Find(c.Id));break;
                case SessionCommandKind.DrawLoan:return s.Finance.Banking.DrawLoan((decimal)c.Amount,(int)c.Start,c.Text,(decimal)c.Value);
                case SessionCommandKind.RenameCompany:s.Simulation.Company.Rename(c.Text);break;
                case SessionCommandKind.StartResearch:s.Research.System.Start(c.Id,s.Simulation.Time.TotalHours);break;
                case SessionCommandKind.SetAutomation:s.Research.SetAutomation(c.Flag,c.OtherFlag);break;
                case SessionCommandKind.OpenOffice:return s.Expansion.OpenOffice(c.Id,c.Flag);
                case SessionCommandKind.HireCrew:return s.Expansion.HireRegionalCrew(c.Id);
                case SessionCommandKind.OrderSupplyPlan:return SupplyPlanner.Purchase(s,c.Id,(SupplyPreference)c.Start,c.Amount);
                case SessionCommandKind.GenerateOpportunities:return s.Expansion.GenerateOpportunities(c.Id);
                case SessionCommandKind.AcquireEquipment:s.Development.AcquireEquipment(c.Id,c.Flag);break;
                case SessionCommandKind.ReleaseEquipment:s.Development.ReleaseEquipment(c.Id);break;
                case SessionCommandKind.SetWorkPolicy:s.Development.SetWorkPolicy(c.Id,c.Flag,c.OtherFlag);break;
                case SessionCommandKind.ProposeChangeOrder:return s.ChangeOrders.Propose(c.Id);
                case SessionCommandKind.NegotiateChangeOrder:s.ChangeOrders.Negotiate(c.Id);break;
                case SessionCommandKind.DecideChangeOrder:s.ChangeOrders.Decide(c.Id,c.Flag);break;
                case SessionCommandKind.SetInsurance:s.Finance.Insurance.SetCoverage((ConstructionEmpire.Systems.Finance.InsuranceType)c.Start,c.Flag);break;
                default:throw new ArgumentException("Unknown saved player command.");
            }
            return null;
        }
        public SessionSaveData Capture()
        {
            if(invalidReason!=null)throw new InvalidOperationException("This diagnostic session cannot be saved: "+invalidReason+". Start or load a regular company first.");
            if(initial==null || executing)throw new InvalidOperationException("This session is not ready for a complete save.");
            return new SessionSaveData {Initial=SessionJson.Copy(initial),Commands=SessionJson.Copy(commands),Clock=session.Simulation.Time.CaptureState(),StateProof=SessionStateProof.Compute(session)};
        }
    }
}
