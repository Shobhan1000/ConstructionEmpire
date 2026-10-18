using System.IO;
using System.Linq;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Procurement;
using ConstructionEmpire.Definitions;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class StarterSupplierGenerator
    {
        [MenuItem("Tools/Project Setup/Generate Starter Suppliers")]
        public static void Generate()
        {
            const string suppliers="Assets/Data/Suppliers", materials="Assets/Data/Materials";
            Directory.CreateDirectory(suppliers);Directory.CreateDirectory(materials);AssetDatabase.Refresh();int created=0,skipped=0;
            foreach(var profile in SampleSupplierDataFactory.CreateStarterSuppliers())
            {
                string path=suppliers+"/"+profile.Id+".asset";
                if(AssetDatabase.LoadAssetAtPath<Object>(path)!=null) {skipped++;continue;}
                var asset=ScriptableObject.CreateInstance<SupplierDefinition>();asset.Profile=profile;AssetDatabase.CreateAsset(asset,path);created++;
            }
            foreach(var m in ManagementSettings.CreateStarter().Materials)
            {
                string path=materials+"/"+m.Id+".asset";if(AssetDatabase.LoadAssetAtPath<Object>(path)!=null) {skipped++;continue;}
                var asset=ScriptableObject.CreateInstance<MaterialDefinition>();asset.Id=m.Id;asset.DisplayName=m.Name;asset.Unit=m.Unit;asset.PricePerUnit=m.Price;asset.Availability=m.Availability;asset.Quality=m.Quality;asset.StorageRequirement=m.StorageRequirement;asset.LeadHours=m.LeadHours;AssetDatabase.CreateAsset(asset,path);created++;
            }
            foreach(var guid in AssetDatabase.FindAssets("t:ManagementConfiguration"))
            {
                var config=AssetDatabase.LoadAssetAtPath<ManagementConfiguration>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var guidSupplier in AssetDatabase.FindAssets("t:SupplierDefinition",new[]{suppliers}).OrderBy(x=>x))
                {var supplier=AssetDatabase.LoadAssetAtPath<SupplierDefinition>(AssetDatabase.GUIDToAssetPath(guidSupplier));if(!config.Suppliers.Contains(supplier))config.Suppliers.Add(supplier);}
                foreach(var guidMaterial in AssetDatabase.FindAssets("t:MaterialDefinition",new[]{materials}).OrderBy(x=>x))
                {var material=AssetDatabase.LoadAssetAtPath<MaterialDefinition>(AssetDatabase.GUIDToAssetPath(guidMaterial));if(!config.Materials.Contains(material))config.Materials.Add(material);}
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(suppliers);
            Debug.Log($"Starter suppliers/materials: created {created}, skipped {skipped}. Supplier references wired to management configurations.");
        }
    }
}
