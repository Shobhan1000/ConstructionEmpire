using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class BuildWindowsRelease
    {
        [MenuItem("Tools/Release/Build Windows Candidate")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new InvalidOperationException("Stop Play Mode and finish compilation before building.");
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Documents","Codex","2026-09-13","files-mentioned-by-the-user-construction","outputs","Windows-Candidate");
            Directory.CreateDirectory(folder);
            PlayerSettings.productName="Construction Empire";PlayerSettings.bundleVersion="0.9.0";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.forceSingleInstance=true;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/SampleScene.unity"},locationPathName=Path.Combine(folder,"ConstructionEmpire.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            var text="Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nSize: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime;
            foreach(var step in report.steps)foreach(var message in step.messages)if(message.type==LogType.Warning || message.type==LogType.Error)text+="\n"+message.type+": "+message.content;
            File.WriteAllText(Path.Combine(folder,"BuildReport.txt"),text);Debug.Log(text);
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Windows candidate build failed; inspect BuildReport.txt and Console.");
        }
    }
}
