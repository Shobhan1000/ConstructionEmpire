using System;

namespace ConstructionEmpire.Data
{
    /// <summary>Cost breakdown behind a bid, for showing the player a line-item estimate before they set a price (spec section 3).</summary>
    [Serializable]
    public class BidEstimateData
    {
        public double LaborCost;
        public double MaterialCost;
        public double EquipmentCost;
        public double Overhead;
        public float ContingencyPercent;
        public double ContingencyAmount;
        public float DesiredProfitPercent;

        public double Subtotal => LaborCost + MaterialCost + EquipmentCost + Overhead;
        public double RecommendedBidPrice => (Subtotal + ContingencyAmount) * (1.0 + DesiredProfitPercent);
    }
}
