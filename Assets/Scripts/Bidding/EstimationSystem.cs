using System.Collections.Generic;
using ConstructionEmpire.Content;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Bidding
{
    /// <summary>
    /// Turns a project's phase list into a cost breakdown and recommended
    /// bid price (spec section 3). Pure calculation, no side effects - safe
    /// to call repeatedly as the player adjusts contingency/profit inputs
    /// in the (future) bidding UI.
    /// </summary>
    public class EstimationSystem
    {
        private readonly ContentLibrary _content;

        public EstimationSystem(ContentLibrary content)
        {
            _content = content;
        }

        public BidEstimateData Estimate(ProjectTemplate template, IReadOnlyList<string> phaseIds, float contingencyPercent, float desiredProfitPercent)
        {
            var estimate = new BidEstimateData
            {
                ContingencyPercent = contingencyPercent,
                DesiredProfitPercent = desiredProfitPercent
            };

            foreach (var phaseId in phaseIds)
            {
                var phase = _content.GetPhase(phaseId);
                if (phase == null) continue;

                estimate.LaborCost += phase.BaseLaborCost;

                foreach (var req in phase.RequiredMaterials)
                {
                    var material = _content.GetMaterial(req.Type);
                    if (material != null)
                    {
                        estimate.MaterialCost += req.Quantity * material.BasePricePerUnit;
                    }
                }

                foreach (var req in phase.RequiredEquipment)
                {
                    var equipment = _content.GetEquipment(req.Type);
                    if (equipment != null)
                    {
                        estimate.EquipmentCost += req.DaysNeeded * equipment.DailyRentalPrice;
                    }
                }
            }

            double preOverheadSubtotal = estimate.LaborCost + estimate.MaterialCost + estimate.EquipmentCost;
            double overheadFraction = template != null ? template.OverheadFraction : 0.12;
            estimate.Overhead = preOverheadSubtotal * overheadFraction;
            estimate.ContingencyAmount = (preOverheadSubtotal + estimate.Overhead) * contingencyPercent;

            return estimate;
        }
    }
}
