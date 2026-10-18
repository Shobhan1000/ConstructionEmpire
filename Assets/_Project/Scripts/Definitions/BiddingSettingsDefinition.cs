using ConstructionEmpire.Data.Bidding;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Bidding Settings")]
    public sealed class BiddingSettingsDefinition : ScriptableObject
    {
        public EstimationRates Rates;
        public BiddingConfiguration Bidding;
        [Range(0,1)] public float DefaultProfitFraction;
        [Range(0,1)] public float DefaultRiskFraction;
    }
}
