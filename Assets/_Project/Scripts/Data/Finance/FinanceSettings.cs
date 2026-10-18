using System;
namespace ConstructionEmpire.Data.Finance
{
    [Serializable]
    public sealed class FinanceSettings
    {
        public double UpfrontFraction=.1,ProgressFraction=.4,RetentionFraction=.05;
        public double ProgressPaymentThreshold=50;
        public int ProgressPaymentDelayDays=3,FinalPaymentDelayDays=3,WarrantyDays=28,WagePaymentDays=7;
        public double MaximumPenaltyFraction=.1,ProjectOverheadPerWorkHour=5,OfficeCostPerDay=35;
        public double EquipmentUsefulLifeHours=10000,LowCashThreshold=10000;
        public double BaseAnnualInterestRate=.08,PoorCreditRatePremium=.12,CreditLimit=150000,CollateralRequiredFraction=.25;
        public int StartingCreditScore=700,MinimumCreditScore=400,LoanTermMonths=12;
        public double InsurancePremium=250,InsuranceCoverageFraction=.75,InsuranceDeductible=500,TaxRate=.2,TaxReserveFraction=.2;
        public double[] PolicyPremiumMultipliers={1,1.2,1.5,2};
        public int InsuranceClaimDelayDays=3;
        public int InsuranceCadenceDays=28,TaxCadenceDays=28;
        public float ProfitableReputation=2,LossReputation=-3,OnTimeReputation=2,LateReputation=-3,QualityMetReputation=1,QualityMissedReputation=-3;
        public double DebugContractValue=1000000,DebugCost=400000,DebugProgress=60;
        public int DebugPaymentDelayDays=7;
        public void Validate()
        {
            if(InsuranceClaimDelayDays<0 || PolicyPremiumMultipliers==null || PolicyPremiumMultipliers.Length!=4)throw new ArgumentException("Configure four insurance policies.");
            foreach(var multiplier in PolicyPremiumMultipliers)if(double.IsNaN(multiplier)||double.IsInfinity(multiplier)||multiplier<0)throw new ArgumentException("Invalid insurance premium.");
            foreach(double value in new[]{UpfrontFraction,ProgressFraction,RetentionFraction,ProgressPaymentThreshold,MaximumPenaltyFraction,ProjectOverheadPerWorkHour,OfficeCostPerDay,
                EquipmentUsefulLifeHours,LowCashThreshold,BaseAnnualInterestRate,PoorCreditRatePremium,CreditLimit,CollateralRequiredFraction,InsurancePremium,InsuranceCoverageFraction,InsuranceDeductible,TaxRate,TaxReserveFraction,DebugContractValue,DebugCost,DebugProgress})
                if(double.IsNaN(value) || double.IsInfinity(value) || value<0) throw new ArgumentException("Finance settings must be finite and non-negative.");
            if(UpfrontFraction+ProgressFraction>=1 || RetentionFraction>=1 || ProgressPaymentThreshold<=0 || ProgressPaymentThreshold>=100 || MaximumPenaltyFraction>1 || InsuranceCoverageFraction>1 || TaxRate>1 || TaxReserveFraction>1 || CollateralRequiredFraction>1 || DebugProgress>100) throw new ArgumentException("Invalid finance fraction.");
            if(ProgressPaymentDelayDays<0 || FinalPaymentDelayDays<0 || WarrantyDays<0 || WagePaymentDays<0 || LoanTermMonths<1 || InsuranceCadenceDays<1 || TaxCadenceDays<1 || DebugPaymentDelayDays<1 || EquipmentUsefulLifeHours<=0 || StartingCreditScore<MinimumCreditScore || MinimumCreditScore<0 || StartingCreditScore>1000) throw new ArgumentException("Invalid cadence, credit score or useful life.");
        }
    }
}
