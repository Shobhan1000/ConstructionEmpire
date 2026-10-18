using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Research;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Research Upgrade")]
    public sealed class ResearchUpgradeDefinition : ScriptableObject
    {
        public ResearchUpgradeData Data=new ResearchUpgradeData();
        public List<ResearchUpgradeDefinition> Prerequisites=new List<ResearchUpgradeDefinition>();
        public ResearchUpgradeData CreateRuntimeData()
        {if(Prerequisites.Any(x=>x==null))throw new System.InvalidOperationException("Missing research prerequisite asset.");var data=Data.Copy();data.PrerequisiteIds=Prerequisites.Select(x=>x.Data.Id).ToList();data.Validate();return data;}
    }
}
