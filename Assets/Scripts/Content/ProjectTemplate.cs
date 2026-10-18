using QualityLevel = ConstructionEmpire.Data.QualityLevel;
using UnityEngine;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Content
{
    /// <summary>A category of project opportunity (spec section 2). Runtime opportunities are randomized instances generated from one of these.</summary>
    [CreateAssetMenu(fileName = "ProjectTemplate", menuName = "ConstructionEmpire/Content/Project Template")]
    public class ProjectTemplate : ScriptableObject
    {
        public ProjectType Type;
        public string DisplayName;
        [Tooltip("PhaseIds (from ConstructionPhaseDefinition) this project type requires, in a sensible default order.")]
        public string[] PhaseIds;
        public double MinContractValue = 50000;
        public double MaxContractValue = 150000;
        public QualityLevel RequiredQuality = QualityLevel.Average;
        [Range(0f, 1f)] public float BaseSiteDifficulty = 0.3f;
        [Tooltip("Overhead applied on top of labor+material+equipment cost during estimation, as a fraction (0.1 = 10%).")]
        [Range(0f, 0.5f)] public float OverheadFraction = 0.12f;
    }
}
