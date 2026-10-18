using System;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Finance;
namespace ConstructionEmpire.Systems.Finance
{
    public readonly struct ProjectProfitability
    {
        public decimal CurrentCosts {get;} public decimal RemainingEstimatedCosts {get;} public decimal ForecastCost=>CurrentCosts+RemainingEstimatedCosts;
        public decimal CurrentProfit {get;} public decimal ForecastProfit {get;} public decimal ProfitMargin {get;} public decimal BudgetVariance {get;}
        public ProjectProfitability(decimal cost,decimal remaining,decimal earned,decimal value,decimal budget)
        { CurrentCosts=cost;RemainingEstimatedCosts=remaining;CurrentProfit=earned-cost;ForecastProfit=value-cost-remaining;ProfitMargin=value==0 ? 0 : ForecastProfit/value;BudgetVariance=budget-cost-remaining; }
    }
    public static class ProjectProfitabilityCalculator
    {
        public static ProjectProfitability Calculate(ProjectData project,ContractData contract,decimal accruedCosts,decimal estimatedRemainingCosts)
        {
            if(project==null || contract==null || project.Id!=contract.ProjectId || accruedCosts<0 || estimatedRemainingCosts<0) throw new ArgumentException("Invalid profitability inputs.");
            return new ProjectProfitability(accruedCosts,estimatedRemainingCosts,contract.RevenueRecognized,contract.ContractValue,(decimal)project.Budget);
        }
        public static void ApplyCompletionReputation(CompanyData company,ContractData contract,decimal finalProfit,FinanceSettings settings)
        {
            if(!contract.Completed || contract.ReputationApplied) return;
            float delta=(finalProfit>=0 ? settings.ProfitableReputation : settings.LossReputation)+
                (contract.CompletionHour<=contract.CompletionDeadlineHour ? settings.OnTimeReputation : settings.LateReputation)+
                (contract.QualityOutcome>=contract.RequiredQuality ? settings.QualityMetReputation : settings.QualityMissedReputation);
            contract.ReputationApplied=true;
            company.SetReputation(Math.Max(0,Math.Min(100,company.Reputation+delta)),"Project result: profit, schedule and quality for "+contract.ProjectId);
        }
    }
}
