using System.IO;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Definitions;
using ConstructionEmpire.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class ConfigureSiteVisuals
    {
        [MenuItem("Tools/Project Setup/Configure Site Visuals")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Configure site visuals outside Play Mode.");return;}
            const string folder="Assets/Data/SiteVisuals",path=folder+"/DefaultSiteVisuals.asset";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var definition=AssetDatabase.LoadAssetAtPath<SiteVisualDefinition>(path);
            if(definition==null){if(AssetDatabase.LoadAssetAtPath<Object>(path)!=null)throw new System.InvalidOperationException("Conflicting asset at "+path);definition=ScriptableObject.CreateInstance<SiteVisualDefinition>();AssetDatabase.CreateAsset(definition,path);}
            var dashboard=Object.FindFirstObjectByType<MainDashboardController>();
            if(dashboard==null){Debug.LogWarning("Attach Dashboard Controllers first. The site-visual definition has been created.");return;}
            var controller=dashboard.GetComponent<SiteVisualController>()??Undo.AddComponent<SiteVisualController>(dashboard.gameObject);Undo.RecordObject(controller,"Configure site visuals");
            if(controller.Definition==null)controller.Definition=definition;controller.Bootstrap=dashboard.Bootstrap!=null?dashboard.Bootstrap:Object.FindFirstObjectByType<SimulationBootstrapper>();EditorUtility.SetDirty(controller);EditorSceneManager.MarkSceneDirty(dashboard.gameObject.scene);
            AssetDatabase.SaveAssets();Selection.activeObject=controller;Debug.Log("Site visuals configured. Six stage slots and assignment badges are ready; labelled placeholders remain until art is assigned. Existing visual assets preserved.");
        }
    }
}
