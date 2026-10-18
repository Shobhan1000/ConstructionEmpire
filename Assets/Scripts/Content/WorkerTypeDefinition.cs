using UnityEngine;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Content
{
    [CreateAssetMenu(fileName = "WorkerTypeDefinition", menuName = "ConstructionEmpire/Content/Worker Type Definition")]
    public class WorkerTypeDefinition : ScriptableObject
    {
        public WorkerType Type;
        public string DisplayName;
        public double BaseDailyWage;
        [Range(0.1f, 3f)] public float BaseProductivity = 1f;
    }
}
