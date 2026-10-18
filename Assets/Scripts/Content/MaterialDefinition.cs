using UnityEngine;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Content
{
    [CreateAssetMenu(fileName = "MaterialDefinition", menuName = "ConstructionEmpire/Content/Material Definition")]
    public class MaterialDefinition : ScriptableObject
    {
        public MaterialType Type;
        public string DisplayName;
        public string Unit = "unit";
        public double BasePricePerUnit;
        public int DefaultLeadTimeDays = 3;
    }
}
