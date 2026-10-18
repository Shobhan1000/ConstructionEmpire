using System;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
namespace ConstructionEmpire.Systems.Finance
{
    /// <summary>Liquid cash only. Revenue recognition, loans and depreciation never become interchangeable.</summary>
    public sealed class CashFlowTracker : IDisposable
    {
        private readonly CompanyData company;
        private readonly FinanceSaveData state;
        private readonly decimal threshold;
        private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private bool projecting;
        private int warningLevel;
        public decimal Cash=>state.Cash;
        public decimal NetCashFlow=>state.Cash-state.OpeningCash;
        public bool IsCashPoor=>Cash<threshold;
        public bool IsOverdrawn=>Cash<0;
        public CashFlowTracker(CompanyData company,FinanceSaveData state,decimal lowCashThreshold)
        {
            this.company=company; this.state=state; threshold=lowCashThreshold;
            subscriptions.Subscribe<BalanceChangedEvent>(e=> {
                if(!projecting && ReferenceEquals(e.Company,company)) { state.Cash=(decimal)e.CurrentBalance; CheckWarning(); }
            });
        }
        public void Apply(decimal amount,string reason)
        {
            decimal next=checked(state.Cash+amount);
            state.Cash=next;
            projecting=true;
            try { company.SetFinanceCashBalance((float)next,reason); }
            finally { projecting=false; }
            CheckWarning();
        }
        private void CheckWarning()
        {
            int level=Cash<0 ? 2 : Cash<threshold ? 1 : 0;
            if(level>0 && level!=warningLevel) { warningLevel=level; GameEvents.Publish(new CashFlowWarningEvent(company,Cash,level==2)); }
            else warningLevel=level;
        }
        public decimal ForecastNetCash(decimal futureReceipts,decimal futurePayments)=>Cash+futureReceipts-futurePayments;
        public void Dispose()=>subscriptions.Dispose();
    }
}
