using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Procurement;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/MaterialDefinition")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        public string Id,DisplayName,Unit,StorageRequirement;
        public double PricePerUnit=1,Availability=10000,Quality=.8,PriceFluctuationTendency=.03;
        public int LeadHours=4;
        public MaterialCatalogEntry CreateRuntimeData()=>new MaterialCatalogEntry {Id=Id,Name=DisplayName,Unit=Unit,StorageRequirement=StorageRequirement,Price=PricePerUnit,Availability=Availability,Quality=Quality,LeadHours=LeadHours,Supplier="Choose supplier"};
    }
}
