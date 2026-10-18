using ConstructionEmpire.Data.Construction;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/EmployeeAsset")]
    public sealed class EmployeeAsset : ScriptableObject
    {
        public EmployeeData Data;
        public EmployeeData CreateRuntimeData() { Data.Validate(); return Data.Copy(); }
    }
}
