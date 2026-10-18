using System.IO;
using UnityEditor;
using UnityEngine;

namespace ConstructionEmpire.Editor
{
    public static class VerifyTestEnvironment
    {
        public const string TestAssemblyPath =
            "Assets/_Project/Tests/EditMode/ConstructionEmpire.Tests.EditMode.asmdef";

        [MenuItem("Tools/Project Setup/Verify Test Environment")]
        public static void Verify()
        {
            string fullPath = Path.Combine(Application.dataPath, TestAssemblyPath.Substring("Assets/".Length));
            Debug.Log(File.Exists(fullPath)
                ? $"PASS: Edit-mode test assembly exists at {TestAssemblyPath}."
                : $"FAIL: Edit-mode test assembly is missing at {TestAssemblyPath}.");

            bool frameworkFound = false;
            foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (package.name != "com.unity.test-framework") continue;
                frameworkFound = true;
                Debug.Log($"[x] Required package: Unity Test Framework (com.unity.test-framework), installed {package.version}.");
                break;
            }
            if (!frameworkFound)
                Debug.Log("[ ] Required package: Unity Test Framework (com.unity.test-framework) is missing.");
            Debug.Log("Checklist: confirm Test Framework in Window > Package Manager; " +
                "open Window > General > Test Runner; select EditMode; run ConstructionEmpire.Tests.EditMode.");
        }
    }
}
