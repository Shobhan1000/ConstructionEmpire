using System;

namespace ConstructionEmpire.Data
{
    /// <summary>A persistent client the company can do repeat business with (spec sections 30-31).</summary>
    [Serializable]
    public class ClientData
    {
        public string Id;
        public string Name;
        public ClientType Type;

        /// <summary>0-1. Stored for future bidding/negotiation tuning - not yet wired into the win-probability formula itself, to avoid disturbing its already-tested price/reputation weighting.</summary>
        public float PriceSensitivity;
        public QualityLevel QualityExpectation = QualityLevel.Average;
        public float RelationshipScore = 40f; // 0-100

        public RelationshipLevel GetRelationshipLevel()
        {
            if (RelationshipScore >= 85f) return RelationshipLevel.Strategic;
            if (RelationshipScore >= 65f) return RelationshipLevel.Preferred;
            if (RelationshipScore >= 45f) return RelationshipLevel.Trusted;
            if (RelationshipScore >= 20f) return RelationshipLevel.Established;
            return RelationshipLevel.New;
        }
    }
}
