using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    [InitializeOnLoad]public static class ReleaseVerification
    {
        private static TestRunnerApi runner;
        private const string Request="Temp/ConstructionEmpire-Release.request";
        private static double nextPoll;
        static ReleaseVerification(){EditorApplication.delayCall+=Pending;EditorApplication.update+=Poll;}
        private static void Poll(){if(EditorApplication.timeSinceStartup<nextPoll || EditorApplication.isPlayingOrWillChangePlaymode)return;nextPoll=EditorApplication.timeSinceStartup+1;Pending();}
        private static void Pending()
        {
            if(!File.Exists(Request))return;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
            var mode=File.ReadAllText(Request).Trim();File.Move(Request,Request+".consumed-"+DateTime.UtcNow.Ticks);
            if(mode=="tests")RunTests();else if(mode=="build")BuildWindowsRelease.Build();else if(mode=="smoke")ReleasePlaySmoke.Run();
        }
        [MenuItem("Tools/Release/Run Regression Tests")]
        public static void RunTests()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results());
            runner.Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode}));
        }
        private sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor test){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,Path.GetFullPath("Temp/Release-TestResults.xml"));
                Debug.Log("Release regression tests: "+result.PassCount+" passed, "+result.FailCount+" failed.");
            }
        }
    }
}
