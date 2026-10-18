using ConstructionEmpire.Data.Construction;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/EquipmentAsset")]
    public sealed class EquipmentAsset : ScriptableObject
    {
        public EquipmentRuntimeData Data;
        public EquipmentRuntimeData CreateRuntimeData() { Data.Validate(); return Data.Copy(); }
    }
}
