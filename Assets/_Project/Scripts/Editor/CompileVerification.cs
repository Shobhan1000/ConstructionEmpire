using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    [InitializeOnLoad]
    public static class CompileVerification
    {
        private const string Report="Temp/Milestone4-Compile.txt";
        static CompileVerification()
        {
            CompilationPipeline.compilationStarted+=_=> {SessionState.SetInt("CE.Compile.Errors",0);SessionState.SetInt("CE.Compile.Warnings",0);};
            CompilationPipeline.assemblyCompilationFinished+=OnAssembly;
            CompilationPipeline.compilationFinished+=OnFinished;
        }
        [MenuItem("Tools/Project Setup/Run Compile Verification")]
        public static void Scan()
        {
            File.WriteAllText(Report,"Compilation requested.\n"); SessionState.SetInt("CE.Compile.Errors",0); SessionState.SetInt("CE.Compile.Warnings",0);
            AssetDatabase.Refresh(); CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }
        private static void OnAssembly(string path,CompilerMessage[] messages)
        {
            SessionState.SetInt("CE.Compile.Errors",SessionState.GetInt("CE.Compile.Errors",0)+messages.Count(x=>x.type==CompilerMessageType.Error));
            SessionState.SetInt("CE.Compile.Warnings",SessionState.GetInt("CE.Compile.Warnings",0)+messages.Count(x=>x.type==CompilerMessageType.Warning));
            foreach(var message in messages) File.AppendAllText(Report,message.type+": "+message.message+"\n");
        }
        private static void OnFinished(object context)
        {
            string result="Compilation finished: "+SessionState.GetInt("CE.Compile.Errors",0)+" errors, "+SessionState.GetInt("CE.Compile.Warnings",0)+" warnings.";
            File.AppendAllText(Report,result+"\n"); Debug.Log(result);
        }
    }
}
