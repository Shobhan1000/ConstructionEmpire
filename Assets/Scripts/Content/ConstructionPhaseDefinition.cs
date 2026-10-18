using System;
using UnityEngine;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Content
{
    [Serializable]
    public class MaterialRequirement
    {
        public MaterialType Type;
        public float Quantity;
    }

    [Serializable]
    public class EquipmentRequirement
    {
        public EquipmentType Type;
        public int DaysNeeded = 1;
    }

    /// <summary>
    /// A reusable phase template (spec section 6). Several ProjectTemplates
    /// can share the same phase (e.g. "framing" looks the same whether it's
    /// a house or a warehouse) - keeps the phase library small and
    /// consistent instead of duplicating near-identical phases per project
    /// type.
    /// </summary>
    [CreateAssetMenu(fileName = "ConstructionPhaseDefinition", menuName = "ConstructionEmpire/Content/Construction Phase Definition")]
    public class ConstructionPhaseDefinition : ScriptableObject
    {
        [Tooltip("Stable id referenced by ProjectTemplate.PhaseIds and by other phases' PrerequisitePhaseIds. Never change this once it's used in a save file.")]
        public string PhaseId;
        public string DisplayName;
        public int BaseDurationDays = 5;
        public double BaseLaborCost = 5000;
        [Range(0f, 1f)] public float QualityImpactWeight = 0.1f;
        public WorkerType[] RequiredWorkerTypes;
        public MaterialRequirement[] RequiredMaterials;
        public EquipmentRequirement[] RequiredEquipment;
        [Tooltip("PhaseIds that must be Completed before this phase becomes Ready. Ignored if a listed id isn't part of the current project's phase set.")]
        public string[] PrerequisitePhaseIds;
        [Tooltip("Which subcontractor trade can take this phase over instead of an in-house crew. None = in-house only.")]
        public SubcontractorSpecialty RelevantSpecialty = SubcontractorSpecialty.None;
        [Tooltip("Whether weather affects this phase's daily progress (spec section 23). False for indoor work.")]
        public bool IsOutdoorWork = true;
        [Tooltip("Whether reaching 100% progress triggers a pass/fail inspection roll instead of completing immediately (spec section 22). Only applies to in-house crewed phases - subcontracted work is assumed to pass its own quality control.")]
        public bool RequiresInspection = false;
    }
}
