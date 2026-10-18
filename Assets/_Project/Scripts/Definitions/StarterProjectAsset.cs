using ConstructionEmpire.Data.Bidding;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    /// <summary>Authoring wrapper only. Runtime systems use a copy, never mutate this asset.</summary>
    [CreateAssetMenu(menuName="Construction Empire/Starter Project")]
    public sealed class StarterProjectAsset : ScriptableObject
    {
        public ProjectTemplateDefinition Template;
        public ProjectData Data;
        public ProjectData CreateRuntimeData() { Data.Validate(); return Data.Copy(); }
    }
}
