using System.IO;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Definitions;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class GenerateStarterProjectsAndCompetitors
    {
        public const string ProjectFolder="Assets/Data/StarterProjects";
        public const string CompetitorFolder="Assets/Data/Competitors";
        public const string TemplateFolder="Assets/Data/ProjectTemplates";
        [MenuItem("Tools/Project Setup/Generate Starter Projects & Competitors")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.Log("Exit Play Mode before generating starter assets."); return; }
            EnsureFolder(ProjectFolder);EnsureFolder(CompetitorFolder);EnsureFolder(TemplateFolder);
            int created=0,skipped=0;
            var projects=SampleProjectDataFactory.CreateStarterProjects();
            for(int i=0;i<projects.Count;i++)
            {
                var data=projects[i];data.ClientName=BiddingFlavorContent.Clients[i%BiddingFlavorContent.Clients.Length];
                string templatePath=TemplateFolder+"/"+data.TemplateId+".asset";
                ProjectTemplateDefinition template;
                if (Exists(templatePath)) { skipped++;template=AssetDatabase.LoadAssetAtPath<ProjectTemplateDefinition>(templatePath); }
                else
                {
                    template=ScriptableObject.CreateInstance<ProjectTemplateDefinition>();
                    template.TemplateId=data.TemplateId;template.BuildingType=data.BuildingType;
                    // Exact authored starter size; designers may widen the range on the generated template.
                    template.MinimumSize=data.Size;template.MaximumSize=data.Size;template.ReferenceSize=data.Size;
                    template.BaseDurationHours=data.EstimatedDurationHours;template.BaseRequirements=data.Requirements;template.QualityRequirement=data.QualityRequirement;
                    AssetDatabase.CreateAsset(template,templatePath);created++;
                }
                string path=ProjectFolder+"/"+data.Id+".asset";
                if (Exists(path)) { skipped++;continue; }
                if (template==null) { Debug.LogError("Existing template path has incompatible asset type: "+templatePath);continue; }
                var asset=ScriptableObject.CreateInstance<StarterProjectAsset>();asset.Template=template;
                asset.Data=template.CreateProject(data.Id,data.Title,data.Region,data.Location,data.ClientId,data.ClientName,data.Size,data.Budget,
                    data.DeadlineHour,data.PenaltyPerLateDay,data.PaymentSchedule);
                AssetDatabase.CreateAsset(asset,path);created++;
            }
            foreach(var data in SampleProjectDataFactory.CreateCompetitors())
            {
                string path=CompetitorFolder+"/"+data.Id+".asset";
                if (Exists(path)) { skipped++;continue; }
                data.Validate();var asset=ScriptableObject.CreateInstance<CompetitorCompanyAsset>();asset.Data=data;
                AssetDatabase.CreateAsset(asset,path);created++;
            }
            const string settingsPath="Assets/Data/BiddingSettings.asset";
            if (Exists(settingsPath)) skipped++;
            else
            {
                var settings=ScriptableObject.CreateInstance<BiddingSettingsDefinition>();settings.Rates=SampleProjectDataFactory.CreateRates();
                settings.Bidding=SampleProjectDataFactory.CreateBiddingConfiguration();settings.DefaultProfitFraction=.12f;settings.DefaultRiskFraction=.08f;
                AssetDatabase.CreateAsset(settings,settingsPath);created++;
            }
            AssetDatabase.SaveAssets();
            Selection.objects=new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(ProjectFolder),AssetDatabase.LoadAssetAtPath<DefaultAsset>(CompetitorFolder) };
            Debug.Log($"Starter generation complete: {created} assets created, {skipped} existing assets skipped. Provisional values are editable in Assets/Data.");
        }
        private static bool Exists(string path) => File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path)!=null;
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
