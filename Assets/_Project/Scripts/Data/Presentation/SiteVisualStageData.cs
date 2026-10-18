using System;
using System.Collections.Generic;
namespace ConstructionEmpire.Data.Presentation
{
    [Serializable] public sealed class SiteVisualStageData
    {
        public string Name;public double MinimumProgress;public bool RequiresProjectComplete;
        public List<string> RequiredCompletedPhaseIds=new List<string>();
        public SiteVisualStageData Copy()=>new SiteVisualStageData {Name=Name,MinimumProgress=MinimumProgress,RequiresProjectComplete=RequiresProjectComplete,RequiredCompletedPhaseIds=new List<string>(RequiredCompletedPhaseIds)};
        public static List<SiteVisualStageData> CreateDefaults()=>new List<SiteVisualStageData>
        {
            new SiteVisualStageData {Name="Site preparation",MinimumProgress=0},
            new SiteVisualStageData {Name="Foundation poured",MinimumProgress=20},
            new SiteVisualStageData {Name="Framed",MinimumProgress=40},
            new SiteVisualStageData {Name="Roofed",MinimumProgress=60},
            new SiteVisualStageData {Name="Exterior complete",MinimumProgress=85},
            new SiteVisualStageData {Name="Finished",MinimumProgress=100,RequiresProjectComplete=true}
        };
    }
}
