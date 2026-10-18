using UnityEngine;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Content
{
    /// <summary>A subcontractor company that can take a phase off a crew's hands, for a price (spec section 18).</summary>
    [CreateAssetMenu(fileName = "SubcontractorDefinition", menuName = "ConstructionEmpire/Content/Subcontractor Definition")]
    public class SubcontractorDefinition : ScriptableObject
    {
        [Tooltip("Stable id. Never change once used in a save file.")]
        public string Id;
        public string CompanyName;
        public SubcontractorSpecialty Specialty;
        [Tooltip("Multiplier on the phase's BaseLaborCost - 1.15 = 15% more than doing it in-house.")]
        public float BaseCostMultiplier = 1.1f;
        [Range(0f, 1f)] public float BaseReliability = 0.8f;
        [Range(0f, 1f)] public float BaseQuality = 0.75f;
        [Tooltip("Multiplier on the phase's BaseDurationDays when on time - 0.9 = 10% faster than the in-house baseline.")]
        public float SpeedFactor = 1f;
    }
}
