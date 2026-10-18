using ConstructionEmpire.Data.Construction;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Construction Settings")]
    public sealed class ConstructionSettingsDefinition : ScriptableObject
    {
        public CrewEfficiencyConfiguration CrewEfficiency=new CrewEfficiencyConfiguration();
    }
}
