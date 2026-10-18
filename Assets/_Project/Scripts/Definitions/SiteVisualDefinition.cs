using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Presentation;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [Serializable] public sealed class SiteStageVisual
    {public SiteVisualStageData Stage=new SiteVisualStageData();public Sprite Sprite;public GameObject Prefab;}
    [CreateAssetMenu(menuName="Construction Empire/Site Visuals")]
    public sealed class SiteVisualDefinition : ScriptableObject
    {
        public List<SiteStageVisual> Stages=SiteVisualStageData.CreateDefaults().Select(x=>new SiteStageVisual {Stage=x}).ToList();
        public Sprite WorkerIcon,EquipmentIcon;
    }
}
