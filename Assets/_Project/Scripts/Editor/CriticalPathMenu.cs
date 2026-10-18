using System;
using System.Linq;
using System.Text;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Definitions;
using ConstructionEmpire.Systems.Scheduling;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class CriticalPathMenu
    {
        [MenuItem("Tools/Debug/Log Critical Path for Selected Project")]
        public static void LogSelected()
        {
            var asset=Selection.activeObject as StarterProjectAsset;
            if(asset==null){Debug.LogWarning("Select a ProjectData-backed StarterProjectAsset in the Project window.");return;}
            try
            {
                var definitions=asset.Template?.Phases;
                if(definitions==null || definitions.Count==0)
                {
                    var bootstrap=UnityEngine.Object.FindFirstObjectByType<SimulationBootstrapper>();
                    var config=bootstrap!=null ? bootstrap.ManagementConfiguration : AssetDatabase.FindAssets("t:ManagementConfiguration").Select(g=>AssetDatabase.LoadAssetAtPath<ManagementConfiguration>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x=>x.Projects.Contains(asset));
                    definitions=config?.DefaultPhases;
                }
                if(definitions==null || definitions.Count==0){Debug.LogWarning("Selected project has no phase template or linked management configuration.");return;}
                var phases=definitions.Select(x=>x.CreateRuntimeData()).ToList();var schedule=CriticalPathCalculator.Calculate(asset.Data.Id,phases,asset.Data.Schedule?.PlannedStart??0);
                var text=new StringBuilder("Critical path for "+asset.Data.Title+" (planned dates; parallel critical branches listed in dependency order):\n");
                foreach(var row in schedule.Phases.Where(x=>x.IsCriticalPath))text.AppendLine(row.Name+" | "+Date(row.PlannedStart)+" → "+Date(row.PlannedCompletion)+" | prerequisites: "+string.Join(", ",phases.Single(x=>x.Id==row.PhaseId).PrerequisiteIds));
                text.AppendLine("Planned completion: "+Date(schedule.PlannedCompletion));Debug.Log(text.ToString(),asset);
            }
            catch(Exception e){Debug.LogError("Cannot calculate selected project's critical path: "+e.Message);}
        }
        private static string Date(double hour)=>"Day "+((long)Math.Floor(hour/24)+1)+", "+(hour%24).ToString("00.##")+":00 (hour "+hour.ToString("0.##")+")";
    }
}
