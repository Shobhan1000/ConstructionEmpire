using System;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Bidding
{
    public enum CompetitorArchetype { LowCost, Premium, Fast, Specialist, Local }
    [Serializable, DataContract]
    public sealed class CompetitorCompanyData : SaveDataBase
    {
        [DataMember] public string Id;
        [DataMember] public string CompanyName;
        [DataMember] public string Personality;
        [DataMember] public CompetitorArchetype Archetype;
        [DataMember] public float PriceMultiplier;
        [DataMember] public float Quality;
        [DataMember] public float DurationMultiplier;
        [DataMember] public float Reputation;
        [DataMember] public float Experience;
        [DataMember] public float ClientRelationship;
        [DataMember] public float Certifications;
        [DataMember] public float MarketFit;
        [DataMember] public string HomeRegion;
        [DataMember] public string Specialty;
        [DataMember] public float RegionalStrength;
        [DataMember] public float CategoryStrength;
        public CompetitorCompanyData Copy() => (CompetitorCompanyData)MemberwiseClone();
        public void Validate()
        {
            BidValidation.Text(Id, nameof(Id)); BidValidation.Text(CompanyName, nameof(CompanyName));
            if (!Enum.IsDefined(typeof(CompetitorArchetype), Archetype)) throw new ArgumentException("Unknown archetype.");
            BidValidation.Positive(PriceMultiplier, nameof(PriceMultiplier)); BidValidation.Positive(DurationMultiplier, nameof(DurationMultiplier));
            BidValidation.Unit(Quality, nameof(Quality)); BidValidation.Unit(Reputation / 100d, nameof(Reputation));
            BidValidation.Unit(Experience, nameof(Experience)); BidValidation.Unit(ClientRelationship, nameof(ClientRelationship));
            BidValidation.Unit(Certifications, nameof(Certifications)); BidValidation.Unit(MarketFit, nameof(MarketFit));
            BidValidation.Unit(RegionalStrength, nameof(RegionalStrength)); BidValidation.Unit(CategoryStrength, nameof(CategoryStrength));
        }
        public override void Migrate()
        { if (SchemaVersion != 1) { base.Migrate(); return; } Validate(); SchemaVersion = 2; }
    }
}
