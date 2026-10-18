using System;
using System.Collections.Generic;

namespace ConstructionEmpire.Data
{
    [Serializable]
    public class PhaseRuntimeData
    {
        public string PhaseId;
        public string DisplayName;
        public PhaseStatus Status = PhaseStatus.Locked;
        public float Progress; // 0-1
        public double CostIncurred;
        public List<string> AssignedCrewIds = new List<string>();
        public List<string> AssignedEquipmentIds = new List<string>();
        public bool IsSubcontracted;
        public string AssignedSubcontractorId;

        /// <summary>Set once, at completion (spec section 20). 0 until then.</summary>
        public float QualityScore;
    }
}
