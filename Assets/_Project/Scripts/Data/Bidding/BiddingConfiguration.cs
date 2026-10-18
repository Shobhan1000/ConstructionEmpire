using System;
namespace ConstructionEmpire.Data.Bidding
{
    [Serializable]
    public sealed class EstimationRates
    {
        public float LaborPerHour;
        public float MaterialPerUnit;
        public float EquipmentPerHour;
        public float SubcontractorPerHour;
        public float OverheadPerHour;
        public float OverheadFraction;
        public void Validate()
        {
            BidValidation.NonNegative(LaborPerHour, nameof(LaborPerHour)); BidValidation.NonNegative(MaterialPerUnit, nameof(MaterialPerUnit));
            BidValidation.NonNegative(EquipmentPerHour, nameof(EquipmentPerHour)); BidValidation.NonNegative(SubcontractorPerHour, nameof(SubcontractorPerHour));
            BidValidation.NonNegative(OverheadPerHour, nameof(OverheadPerHour)); BidValidation.Unit(OverheadFraction, nameof(OverheadFraction));
        }
    }
    [Serializable]
    public sealed class BidWeights
    {
        public float Price, Reputation, Experience, Quality, ClientRelationship, CompletionTime, Certifications, MarketConditions;
        public double Total => (double)Price + Reputation + Experience + Quality + ClientRelationship + CompletionTime + Certifications + MarketConditions;
        public void Validate()
        {
            BidValidation.Positive(Price, nameof(Price)); BidValidation.Positive(Reputation, nameof(Reputation));
            BidValidation.NonNegative(Experience, nameof(Experience)); BidValidation.NonNegative(Quality, nameof(Quality));
            BidValidation.NonNegative(ClientRelationship, nameof(ClientRelationship)); BidValidation.NonNegative(CompletionTime, nameof(CompletionTime));
            BidValidation.NonNegative(Certifications, nameof(Certifications)); BidValidation.NonNegative(MarketConditions, nameof(MarketConditions));
            BidValidation.Positive(Total, nameof(Total));
        }
    }
    [Serializable]
    public sealed class BiddingConfiguration
    {
        public BidWeights Weights;
        public float ScoreSensitivity;
        public float NarrowLossProbabilityGap;
        public float CompetitivePriceThreshold;
        public float PremiumQualityThreshold;
        public float FastDurationThreshold;
        public void Validate()
        {
            if (Weights == null) throw new ArgumentNullException(nameof(Weights));
            Weights.Validate(); BidValidation.Positive(ScoreSensitivity, nameof(ScoreSensitivity));
            BidValidation.Unit(NarrowLossProbabilityGap, nameof(NarrowLossProbabilityGap));
            BidValidation.Positive(CompetitivePriceThreshold, nameof(CompetitivePriceThreshold));
            BidValidation.Unit(PremiumQualityThreshold, nameof(PremiumQualityThreshold));
            BidValidation.Positive(FastDurationThreshold, nameof(FastDurationThreshold));
        }
    }
    /// <summary>All preference inputs except reputation are normalized 0..1. Reputation is 0..100.</summary>
    [Serializable]
    public sealed class BidderCapabilities
    {
        public float Reputation, Experience, Quality, ClientRelationship, Certifications, MarketFit;
        public void Validate()
        {
            BidValidation.Unit(Reputation / 100d, nameof(Reputation)); BidValidation.Unit(Experience, nameof(Experience));
            BidValidation.Unit(Quality, nameof(Quality)); BidValidation.Unit(ClientRelationship, nameof(ClientRelationship));
            BidValidation.Unit(Certifications, nameof(Certifications)); BidValidation.Unit(MarketFit, nameof(MarketFit));
        }
    }
}
