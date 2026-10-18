using System.IO;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Definitions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConstructionEmpire.Editor
{
    public static class CreateSimulationBootstrap
    {
        public const string ConfigurationPath =
            "Assets/_Project/Data/Definitions/SimulationConfiguration.asset";

        [MenuItem("Tools/Project Setup/Create Simulation Bootstrap")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("Exit Play Mode before creating a simulation bootstrap.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("FAIL: Open a scene before creating a simulation bootstrap.");
                return;
            }

            // Includes inactive objects and nested children in the active scene.
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "SimulationBootstrapper" &&
                    candidate.GetComponent<SimulationBootstrapper>() == null) continue;
                Selection.activeGameObject = candidate.gameObject;
                Debug.Log($"SimulationBootstrapper already exists in {scene.name}; selected it.", candidate.gameObject);
                if (candidate.GetComponent<SimulationBootstrapper>() == null)
                    Debug.Log("The named object has no SimulationBootstrapper component; inspect it before configuring.");
                return;
            }

            var configuration = AssetDatabase.LoadAssetAtPath<SimulationConfiguration>(ConfigurationPath);
            if (configuration == null)
            {
                if (File.Exists(ConfigurationPath))
                {
                    Debug.Log($"FAIL: {ConfigurationPath} already exists but is not a SimulationConfiguration.");
                    return;
                }
                EnsureFolder("Assets/_Project/Data/Definitions");
                configuration = ScriptableObject.CreateInstance<SimulationConfiguration>();
                AssetDatabase.CreateAsset(configuration, ConfigurationPath);
                AssetDatabase.SaveAssets();
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Simulation Bootstrap");
            var gameObject = new GameObject("SimulationBootstrapper");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Simulation Bootstrap");
            var bootstrapper = Undo.AddComponent<SimulationBootstrapper>(gameObject);
            bootstrapper.Configure(configuration);
            EditorUtility.SetDirty(bootstrapper);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = gameObject;
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Created SimulationBootstrapper in {scene.name}. " +
                $"Configuration: {ConfigurationPath}. Save the scene and enter Play Mode.", gameObject);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
