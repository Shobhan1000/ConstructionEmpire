using System;
using System.IO;
using System.Linq;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Definitions;
using ConstructionEmpire.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace ConstructionEmpire.Editor
{
    public static class AttachDashboardControllers
    {
        public const string UxmlPath="Assets/_Project/UI/Dashboard/Dashboard.uxml";
        public const string ConfigPath="Assets/Data/ManagementConfiguration.asset";
        [MenuItem("Tools/Project Setup/Attach Dashboard Controllers")]
        public static void Attach()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) { Debug.Log("Exit Play Mode before dashboard setup."); return; }
            var scene=SceneManager.GetActiveScene(); if(!scene.IsValid() || !scene.isLoaded) { Debug.LogError("Open a scene first."); return; }
            var uxml=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if(uxml==null) { Debug.LogError("Dashboard UXML was not found: "+UxmlPath); return; }
            GenerateStarterProjectsAndCompetitors.Generate(); GenerateStarterWorkforceAndEquipment.Generate(); CreateSimulationBootstrap.Create();
            var objects=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
            var bootstrap=objects.Select(x=>x.GetComponent<SimulationBootstrapper>()).FirstOrDefault(x=>x!=null);
            if(bootstrap==null) { Debug.LogError("No SimulationBootstrapper component found. Resolve the named object conflict."); return; }
            var config=AssetDatabase.LoadAssetAtPath<ManagementConfiguration>(ConfigPath);
            if(config==null)
            {
                if(File.Exists(ConfigPath)) { Debug.LogError("Incompatible asset at "+ConfigPath); return; }
                config=ScriptableObject.CreateInstance<ManagementConfiguration>();
                config.Projects=LoadAll<StarterProjectAsset>("Assets/Data/StarterProjects").ToList();
                config.Employees=LoadAll<EmployeeAsset>("Assets/Data/Employees").ToList();
                config.Equipment=LoadAll<EquipmentAsset>("Assets/Data/Equipment").ToList();
                config.Competitors=LoadAll<CompetitorCompanyAsset>("Assets/Data/Competitors").ToList();
                config.Bidding=LoadAll<BiddingSettingsDefinition>("Assets/Data").First();
                config.DefaultPhases=SampleRenovationPhaseFactory.CreateHouseRenovationPhases().Select(x=>AssetDatabase.LoadAssetAtPath<ConstructionPhaseDefinition>("Assets/Data/PhaseTemplates/"+x.Id+".asset")).ToList();
                config.Settings=ManagementSettings.CreateStarter(); AssetDatabase.CreateAsset(config,ConfigPath);
                foreach(var project in config.Projects)
                    if(project.Template!=null && project.Template.Phases.Count==0) { project.Template.Phases=config.DefaultPhases.ToList(); EditorUtility.SetDirty(project.Template); }
                var simulationConfig=AssetDatabase.LoadAssetAtPath<SimulationConfiguration>(CreateSimulationBootstrap.ConfigurationPath);
                if(simulationConfig!=null)
                {
                    if(simulationConfig.StartingCash==100000) simulationConfig.StartingCash=250000;
                    simulationConfig.InitialSpeed=SimulationSpeed.Pause; EditorUtility.SetDirty(simulationConfig);
                }
            }
            var existing=objects.FirstOrDefault(x=>x.name=="DashboardUIRoot"); var target=existing!=null ? existing.gameObject : new GameObject("DashboardUIRoot");
            if(existing==null) { SceneManager.MoveGameObjectToScene(target,scene); Undo.RegisterCreatedObjectUndo(target,"Create dashboard"); }
            var document=GetOrAdd<UIDocument>(target); Undo.RecordObject(document,"Configure dashboard document"); document.visualTreeAsset=uxml;
            if(document.panelSettings==null)
            {
                document.panelSettings=LoadAll<PanelSettings>("Assets").FirstOrDefault();
                if(document.panelSettings==null)
                {
                    Debug.LogWarning("No PanelSettings asset found. Creating DashboardPanelSettings with the default runtime theme.");
                    var panel=ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;
                    panel.referenceResolution=new Vector2Int(1440,900); panel.match=.5f;
                    panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/_Project/UI/Dashboard/Dashboard.tss");
                    AssetDatabase.CreateAsset(panel,"Assets/_Project/UI/Dashboard/DashboardPanelSettings.asset"); document.panelSettings=panel;
                }
            }
            var main=GetOrAdd<MainDashboardController>(target); var notifications=GetOrAdd<NotificationCenter>(target); var detail=GetOrAdd<ProjectDetailController>(target);
            Undo.RecordObjects(new UnityEngine.Object[]{bootstrap,main,notifications,detail},"Wire dashboard controllers");
            bootstrap.ManagementConfiguration=config; main.Document=target.GetComponent<UIDocument>(); main.Bootstrap=bootstrap; main.Notifications=target.GetComponent<NotificationCenter>(); main.ProjectDetail=detail;
            notifications.Document=target.GetComponent<UIDocument>(); notifications.Bootstrap=bootstrap; detail.Document=target.GetComponent<UIDocument>();
            foreach(var obj in new UnityEngine.Object[]{bootstrap,main,notifications,detail,document}) EditorUtility.SetDirty(obj);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); Selection.activeGameObject=target;
            Debug.Log("Dashboard controllers attached and wired. Save the scene, enter Play Mode, choose an opportunity, then use the time controls. Existing assets were preserved.",target);
        }
        private static T GetOrAdd<T>(GameObject target) where T:Component => target.GetComponent<T>()??Undo.AddComponent<T>(target);
        private static T[] LoadAll<T>(string folder) where T:UnityEngine.Object => AssetDatabase.FindAssets("t:"+typeof(T).Name,new[]{folder})
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x,StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x=>x!=null).ToArray();
    }
}
