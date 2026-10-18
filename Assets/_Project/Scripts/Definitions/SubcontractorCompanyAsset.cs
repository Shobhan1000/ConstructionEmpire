using ConstructionEmpire.Data.Subcontractors;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Subcontractor Company")]
    public sealed class SubcontractorCompanyAsset : ScriptableObject
    {
        public SubcontractorCompanyData Data=new SubcontractorCompanyData();
        public SubcontractorCompanyData CreateRuntimeData(){Data.Validate();return Data.Copy();}
    }
}
