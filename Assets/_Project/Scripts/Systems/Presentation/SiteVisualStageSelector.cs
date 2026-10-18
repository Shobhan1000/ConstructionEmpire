using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Presentation;
namespace ConstructionEmpire.Systems.Presentation
{
    /// <summary>Read-only presentation mapping. Does not advance phases or change simulation state.</summary>
    public static class SiteVisualStageSelector
    {
        public static void Validate(IReadOnlyList<SiteVisualStageData> stages)
        {
            if(stages==null || stages.Count==0)throw new ArgumentException("At least one visual stage is required.");double previous=-1;
            foreach(var stage in stages)
            {
                if(stage==null)throw new ArgumentException("Null visual stage.");ConstructionValidation.Text(stage.Name,"Stage name");ConstructionValidation.Percent(stage.MinimumProgress,"Stage threshold");
                if(stage.MinimumProgress<=previous || stage.RequiredCompletedPhaseIds==null || stage.RequiredCompletedPhaseIds.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Stages require increasing thresholds and valid phase IDs.");previous=stage.MinimumProgress;
            }
            if(stages[0].MinimumProgress!=0 || stages[0].RequiresProjectComplete || stages[0].RequiredCompletedPhaseIds.Count!=0)throw new ArgumentException("The initial stage must be an unconditional zero-progress fallback.");
        }
        public static int Select(IReadOnlyList<SiteVisualStageData> stages,double progress,ProjectStatus status,IEnumerable<string> completedPhaseIds)
        {
            Validate(stages);ConstructionValidation.Percent(progress,"Visual progress");var complete=new HashSet<string>(completedPhaseIds??Enumerable.Empty<string>(),StringComparer.Ordinal);int result=0;
            for(int i=1;i<stages.Count;i++)
            {
                var stage=stages[i];if(progress<stage.MinimumProgress || stage.RequiresProjectComplete && status!=ProjectStatus.Complete || stage.RequiredCompletedPhaseIds.Any(id=>!complete.Contains(id)))break;
                result=i;
            }
            return result;
        }
    }
}
