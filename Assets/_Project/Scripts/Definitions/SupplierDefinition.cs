using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Procurement;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/SupplierDefinition")]
    public sealed class SupplierDefinition : ScriptableObject
    {
        public SupplierData Profile=new SupplierData();
        public SupplierData CreateRuntimeData() {Profile.Validate();return Profile.Copy();}
    }
}
