using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConstructionEmpire.Data.Research;
using ConstructionEmpire.Definitions;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class GenerateResearchTreeAssets
    {
        [MenuItem("Tools/Project Setup/Generate Research Tree Assets")]
        public static void Generate()
        {
            const string folder="Assets/Data/Research";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var tree=DefaultResearchTreeFactory.CreateStarterTree();var assets=new Dictionary<string,ResearchUpgradeDefinition>();var created=new List<string>();int skipped=0;
            foreach(var data in tree)
            {
                string path=folder+"/"+data.Id+".asset";var existing=AssetDatabase.LoadAssetAtPath<Object>(path);
                if(existing!=null){var research=existing as ResearchUpgradeDefinition;if(research==null)throw new System.InvalidOperationException("Conflicting asset type: "+path);assets.Add(data.Id,research);skipped++;continue;}
                var asset=ScriptableObject.CreateInstance<ResearchUpgradeDefinition>();asset.Data=data.Copy();AssetDatabase.CreateAsset(asset,path);assets.Add(data.Id,asset);created.Add(data.Id);
            }
            foreach(var id in created){assets[id].Prerequisites=tree.Single(x=>x.Id==id).PrerequisiteIds.Select(x=>assets[x]).ToList();EditorUtility.SetDirty(assets[id]);}
            foreach(var guid in AssetDatabase.FindAssets("t:ManagementConfiguration"))
            {var config=AssetDatabase.LoadAssetAtPath<ManagementConfiguration>(AssetDatabase.GUIDToAssetPath(guid));bool changed=false;foreach(var asset in assets.Values)if(!config.ResearchDefinitions.Contains(asset)){config.ResearchDefinitions.Add(asset);changed=true;}if(changed)EditorUtility.SetDirty(config);}
            AssetDatabase.SaveAssets();Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(folder);Debug.Log($"Research tree: created {created.Count}, skipped {skipped}. Prerequisite and management references wired; existing assets preserved.");
        }
    }
}
