using ConstructionEmpire.Data.Bidding;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Competitor Profile")]
    public sealed class CompetitorCompanyAsset : ScriptableObject
    {
        public CompetitorCompanyData Data;
        public CompetitorCompanyData CreateRuntimeData() { Data.Validate(); return Data.Copy(); }
    }
}
