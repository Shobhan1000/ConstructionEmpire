using System;

namespace ConstructionEmpire.Data
{
    /// <summary>A specific owned or rented machine (spec section 12).</summary>
    [Serializable]
    public class EquipmentRuntimeData
    {
        public string Id;
        public EquipmentType Type;
        public string DisplayName;
        public float Condition = 100f;
        public EquipmentStatus Status = EquipmentStatus.Available;
        public bool IsOwned;
        public string CurrentProjectId;
        public string RentalReturnDateIso; // only set for rented equipment
    }
}
