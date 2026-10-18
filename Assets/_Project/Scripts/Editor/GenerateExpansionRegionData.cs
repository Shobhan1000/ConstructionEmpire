using System.IO;
using System.Linq;
using ConstructionEmpire.Data.Expansion;
using ConstructionEmpire.Definitions;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class GenerateExpansionRegionData
    {
        [MenuItem("Tools/Project Setup/Generate Expansion Region Data")]
        public static void Generate()
        {
            const string folder="Assets/Data/Regions";Directory.CreateDirectory(folder);AssetDatabase.Refresh();int created=0,skipped=0;
            foreach(var data in SampleExpansionDataFactory.CreateStarterRegions())
            {string path=folder+"/"+data.Id+".asset";if(AssetDatabase.LoadAssetAtPath<Object>(path)!=null){skipped++;continue;}var asset=ScriptableObject.CreateInstance<WorldMapRegionAsset>();asset.Data=data;AssetDatabase.CreateAsset(asset,path);created++;}
            foreach(var guid in AssetDatabase.FindAssets("t:ManagementConfiguration"))
            {var config=AssetDatabase.LoadAssetAtPath<ManagementConfiguration>(AssetDatabase.GUIDToAssetPath(guid));bool changed=false;foreach(var id in AssetDatabase.FindAssets("t:WorldMapRegionAsset",new[]{folder}).OrderBy(x=>x)){var region=AssetDatabase.LoadAssetAtPath<WorldMapRegionAsset>(AssetDatabase.GUIDToAssetPath(id));if(!config.Regions.Contains(region)){config.Regions.Add(region);changed=true;}}if(changed)EditorUtility.SetDirty(config);}
            AssetDatabase.SaveAssets();Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(folder);Debug.Log($"Expansion regions: created {created}, skipped {skipped}. Management references wired; existing assets preserved.");
        }
    }
}
