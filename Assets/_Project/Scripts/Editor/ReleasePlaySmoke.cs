using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.UI;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Persistence;
namespace ConstructionEmpire.Editor
{
    [InitializeOnLoad]public static class ReleasePlaySmoke
    {
        private const string Key="CE.ReleaseSmoke";private static int phase;private static double deadline;
        static ReleasePlaySmoke(){if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;}}
        [MenuItem("Tools/Release/Run Save Restore Play Check")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Open the configured SampleScene first.");
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish("FAIL: timed out waiting for game initialization or restoration.");return;}
            if(!EditorApplication.isPlaying)return;
            var b=UnityEngine.Object.FindFirstObjectByType<SimulationBootstrapper>();var ui=UnityEngine.Object.FindFirstObjectByType<MainDashboardController>();
            if(b?.Management==null || ui==null || b.CapturePresentation==null)return;
            try
            {
                if(phase==0)
                {
                    if(!b.Management.Crew.UseShiftPolicy)throw new Exception("New companies must start with regular work/rest shifts.");
                    b.SaveDirectoryOverride=Path.Combine(Application.temporaryCachePath,"ConstructionEmpire-release-verification");b.GameTime.Speed=SimulationSpeed.Pause;
                    ui.Execute(new SessionCommandData {Kind=SessionCommandKind.RenameCompany,Text="Release save verification"});
                    ui.Navigate("Bidding",ui.Session.Projects[0].Id);ui.ReviewTutorialEstimate(ui.Session.Projects[0].Id);
                    b.SaveGame(3);SessionState.SetString(Key+".proof",SessionStateProof.Compute(ui.Session));
                    ui.Execute(new SessionCommandData {Kind=SessionCommandKind.RenameCompany,Text="Unsaved change"});
                    b.LoadGame(3);phase=1;
                }
                else if(phase==1 && !b.IsLoading)
                {
                    if(b.Company.CompanyName!="Release save verification" || SessionStateProof.Compute(ui.Session)!=SessionState.GetString(Key+".proof",""))throw new Exception("Restored simulation differs from saved state: "+b.StorageStatus);
                    if(SessionJson.Deserialize<DashboardSaveData>(b.CapturePresentation()).Step!=2)throw new Exception("Tutorial step was not restored.");
                    ui.Execute(new SessionCommandData {Kind=SessionCommandKind.RenameCompany,Text="Second save"});b.SaveGame(3);b.LoadGame(3,true);phase=2;
                }
                else if(phase==2 && !b.IsLoading)
                {
                    if(b.Company.CompanyName!="Release save verification" || SessionStateProof.Compute(ui.Session)!=SessionState.GetString(Key+".proof",""))throw new Exception("Backup restoration differs from original state.");
                    if(b.GameTime.Speed!=SimulationSpeed.Pause)throw new Exception("Loaded game must be paused.");
                    string manual=File.ReadAllText(b.SlotPath(3));b.SaveGame(0);if(File.ReadAllText(b.SlotPath(3))!=manual || !b.HasSave(0))throw new Exception("Autosave must preserve manual slots.");
                    Finish("PASS: regular new-game shifts, separate autosave and manual slots; game initialized; player command, full save, validated load, tutorial restoration, backup recovery and paused-load behavior verified. Temporary verification slot only.");
                }
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        private static void Finish(string message)
        {File.WriteAllText("Temp/Release-PlaySmoke.txt",message);SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Debug.Log(message);EditorApplication.isPlaying=false;}
    }
}
