using System.IO;
using System.Linq;
using ConstructionEmpire.Data.Subcontractors;
using ConstructionEmpire.Definitions;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class StarterSubcontractorGenerator
    {
        [MenuItem("Tools/Project Setup/Generate Starter Subcontractors")]
        public static void Generate()
        {
            const string folder="Assets/Data/Subcontractors";Directory.CreateDirectory(folder);AssetDatabase.Refresh();int created=0,skipped=0;
            foreach(var data in SampleSubcontractorDataFactory.CreateStarterSubcontractors())
            {
                string path=folder+"/"+data.Id+".asset";if(AssetDatabase.LoadAssetAtPath<Object>(path)!=null){skipped++;continue;}
                var asset=ScriptableObject.CreateInstance<SubcontractorCompanyAsset>();asset.Data=data;AssetDatabase.CreateAsset(asset,path);created++;
            }
            foreach(var guid in AssetDatabase.FindAssets("t:ManagementConfiguration"))
            {
                var config=AssetDatabase.LoadAssetAtPath<ManagementConfiguration>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var companyGuid in AssetDatabase.FindAssets("t:SubcontractorCompanyAsset",new[]{folder}).OrderBy(x=>x))
                {var asset=AssetDatabase.LoadAssetAtPath<SubcontractorCompanyAsset>(AssetDatabase.GUIDToAssetPath(companyGuid));if(!config.Subcontractors.Contains(asset))config.Subcontractors.Add(asset);}
                EditorUtility.SetDirty(config);
            }
            // The six matching starter phases become explicit specialty tasks; dependency IDs are preserved.
            string[] ids={"electrical","plumbing","hvac","roofing","landscaping","framing"};
            foreach(var guid in AssetDatabase.FindAssets("t:ConstructionPhaseDefinition",new[]{"Assets/Data/PhaseTemplates"}))
            {
                var phase=AssetDatabase.LoadAssetAtPath<ConstructionPhaseDefinition>(AssetDatabase.GUIDToAssetPath(guid));int index=System.Array.IndexOf(ids,phase.PhaseId);if(index<0)continue;
                phase.RequiresSubcontractor=true;phase.SubcontractorSpecialty=(SubcontractorSpecialty)index;EditorUtility.SetDirty(phase);
            }
            AssetDatabase.SaveAssets();Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(folder);
            Debug.Log($"Starter subcontractors: created {created}, skipped {skipped}; six specialty phases and management references wired.");
        }
    }
}
