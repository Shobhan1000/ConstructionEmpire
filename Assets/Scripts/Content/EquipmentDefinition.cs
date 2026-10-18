using UnityEngine;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Content
{
    [CreateAssetMenu(fileName = "EquipmentDefinition", menuName = "ConstructionEmpire/Content/Equipment Definition")]
    public class EquipmentDefinition : ScriptableObject
    {
        public EquipmentType Type;
        public string DisplayName;
        public double PurchasePrice;
        public double DailyRentalPrice;
        [Range(0.1f, 3f)] public float BaseProductivity = 1f;
        public float FuelConsumptionPerDay;
        public double DailyMaintenanceCost;
        [Tooltip("Condition lost per day while Working, on a 0-100 scale.")]
        public float ConditionDegradePerWorkingDay = 0.5f;
    }
}
