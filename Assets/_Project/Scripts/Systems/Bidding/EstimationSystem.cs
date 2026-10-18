using System;
using ConstructionEmpire.Data.Bidding;
namespace ConstructionEmpire.Systems.Bidding
{
    public readonly struct CostEstimate
    {
        public double Accuracy {get;}
        public double LowerExpectedCost=>EstimatedCost*Accuracy;
        public double UpperExpectedCost=>EstimatedCost*(2-Accuracy);
        public double Labor { get; }
        public double Materials { get; }
        public double Equipment { get; }
        public double Subcontractors { get; }
        public double Overhead { get; }
        public double EstimatedCost => Labor + Materials + Equipment + Subcontractors + Overhead;
        public double DesiredProfit { get; }
        public double RiskMargin { get; }
        public double BidPrice => EstimatedCost + DesiredProfit + RiskMargin;
        public CostEstimate(double labor, double materials, double equipment, double subcontractors, double overhead,
            double profitFraction, double riskFraction, double accuracy=1)
        {
            Accuracy=accuracy; Labor = labor; Materials = materials; Equipment = equipment; Subcontractors = subcontractors; Overhead = overhead;
            DesiredProfit = (labor + materials + equipment + subcontractors + overhead) * profitFraction;
            RiskMargin = (labor + materials + equipment + subcontractors + overhead) * riskFraction;
        }
    }
    public sealed class ProjectCostDetails
    {
        public double SubcontractorCost,FixedPhaseCost;
    }
    public sealed class EstimationSystem
    {
        private readonly EstimationRates rates;
        // A session can supply authored phase costs without duplicating the bid-price formula.
        public Func<ProjectData,ProjectCostDetails> DetailedCosts {get;set;}
        public double BaseAccuracy {get;set;}=.7;public double ResearchAccuracyBonus {get;set;}
        public double EffectiveAccuracy=>Math.Min(1,BaseAccuracy+ResearchAccuracyBonus);
        public double LaborMarketIndex {get;set;}=1;public double MaterialMarketIndex {get;set;}=1;public double EquipmentMarketIndex {get;set;}=1;
        public EstimationSystem(EstimationRates rates) { this.rates = rates ?? throw new ArgumentNullException(nameof(rates)); rates.Validate(); }
        /// <summary>Profit and risk are fractions of base cost, not of each other. 0.10 = 10%.</summary>
        public CostEstimate Estimate(ProjectData project, double desiredProfitFraction, double riskContingencyFraction)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            project.Validate(); rates.Validate();
            BidValidation.Unit(desiredProfitFraction, nameof(desiredProfitFraction)); BidValidation.Unit(riskContingencyFraction, nameof(riskContingencyFraction));
            var r = project.Requirements;
            double labor = (double)r.LaborHours * rates.LaborPerHour*LaborMarketIndex;
            double materials = (double)r.MaterialUnits * rates.MaterialPerUnit*MaterialMarketIndex;
            double equipment = (double)r.EquipmentHours * rates.EquipmentPerHour*EquipmentMarketIndex;
            var details=DetailedCosts?.Invoke(project);
            double subcontractors = details?.SubcontractorCost ?? (double)r.SubcontractorHours * rates.SubcontractorPerHour;
            if(details!=null){BidValidation.Money(details.SubcontractorCost);BidValidation.Money(details.FixedPhaseCost);}
            double overhead = (labor + materials + equipment + subcontractors) * rates.OverheadFraction +
                (double)project.EstimatedDurationHours * rates.OverheadPerHour+(details?.FixedPhaseCost??0);
            var result = new CostEstimate(labor, materials, equipment, subcontractors, overhead, desiredProfitFraction, riskContingencyFraction, EffectiveAccuracy);
            BidValidation.Money(result.BidPrice);
            return result;
        }
    }
}
