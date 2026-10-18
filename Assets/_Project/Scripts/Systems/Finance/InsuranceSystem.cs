using System;
using System.Linq;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Finance
{
    public enum InsuranceType { GeneralLiability, Equipment, WorkersCompensation, Project }
    /// <summary>Initial general-liability policy. Other policy types reserve extension points for incident systems.</summary>
    public sealed class InsuranceSystem
    {
        private readonly FinanceSystem finance;
        public InsuranceSystem(FinanceSystem finance) { this.finance=finance; }
        public InsuranceType ActivePolicy=>InsuranceType.GeneralLiability;
        public bool HasCoverage(InsuranceType type)=>finance.State.InsurancePolicies.Contains((int)type);
        public decimal PolicyPremium(InsuranceType type)=>FinanceSystem.Money((decimal)(finance.Settings.InsurancePremium*finance.Settings.PolicyPremiumMultipliers[(int)type]));
        public decimal Premium=>finance.State.InsurancePolicies.Sum(x=>PolicyPremium((InsuranceType)x));
        public void SetCoverage(InsuranceType type,bool enabled)
        {
            if(!Enum.IsDefined(typeof(InsuranceType),type))throw new ArgumentException("Unknown insurance policy.");if(HasCoverage(type)==enabled)return;
            if(enabled)
            {
                long cadence=(long)finance.Settings.InsuranceCadenceDays*finance.HoursPerDay;
                var prorated=FinanceSystem.Money(PolicyPremium(type)*Math.Max(0,finance.State.LastInsuranceHour+cadence-finance.Hour)/cadence);
                if(finance.CashFlow.Cash<prorated)throw new InvalidOperationException("Insufficient cash for the initial premium.");
                finance.State.InsurancePolicies.Add((int)type);finance.AccrueExpense(null,prorated,ConstructionEmpire.Data.Finance.FinancialCategory.Insurance,"policy-"+type+"-"+(finance.Ledger.Count+1),"Coverage added: "+type,finance.Hour);
            }
            else finance.State.InsurancePolicies.Remove((int)type);
        }
        public decimal Claim(InsuranceType type,string incident,string project,decimal loss)
        {
            if(loss<0 || string.IsNullOrWhiteSpace(incident))throw new ArgumentException("Invalid insurance claim.");
            string id="claim-"+type+"-"+incident;if(!HasCoverage(type) || finance.State.InsuranceClaims.Contains(id))return 0;
            finance.State.InsuranceClaims.Add(id);decimal amount=EstimatePayout(loss);
            if(amount>0)finance.RecordInsuranceSettlement(project,amount,id,finance.Hour+(long)finance.Settings.InsuranceClaimDelayDays*finance.HoursPerDay);
            return amount;
        }
        public decimal EstimatePayout(decimal loss)
        { if(loss<0) throw new ArgumentOutOfRangeException(nameof(loss)); return FinanceSystem.Money(Math.Max(0,loss-(decimal)finance.Settings.InsuranceDeductible)*(decimal)finance.Settings.InsuranceCoverageFraction); }
        public void OnTick(TickContext context)
        {
            long cadence=(long)finance.Settings.InsuranceCadenceDays*finance.HoursPerDay;
            if(context.TotalHours-finance.State.LastInsuranceHour<cadence) return;
            finance.State.LastInsuranceHour=context.TotalHours;
            finance.AccrueExpense(null,Premium,FinancialCategory.Insurance,"premium-"+context.TotalHours,"Insurance policy premiums",context.TotalHours);
        }
    }
}
