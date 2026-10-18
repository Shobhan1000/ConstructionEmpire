using System;
using System.IO;
using System.Collections.Generic;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Definitions;
using ConstructionEmpire.Systems.Construction;
using UnityEditor;
using UnityEngine;
namespace ConstructionEmpire.Editor
{
    public static class GenerateStarterWorkforceAndEquipment
    {
        public const string EmployeeFolder="Assets/Data/Employees";
        public const string EquipmentFolder="Assets/Data/Equipment";
        public const string PhaseFolder="Assets/Data/PhaseTemplates";
        public const string DefinitionFolder="Assets/Data/EquipmentDefinitions";
        [MenuItem("Tools/Project Setup/Generate Starter Workforce & Equipment")]
        public static void Generate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) { Debug.Log("Exit Play Mode before generating workforce assets."); return; }
            int created=0,skipped=0;
            foreach(var folder in new[] {EmployeeFolder,EquipmentFolder,PhaseFolder,DefinitionFolder}) EnsureFolder(folder);
            var starter=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment();
            foreach(var worker in starter.Workers)
                Create<EmployeeAsset>(EmployeeFolder+"/"+worker.Id+".asset",asset=>asset.Data=worker,ref created,ref skipped);
            foreach(var machine in starter.Equipment)
            {
                Create<EquipmentDefinition>(DefinitionFolder+"/"+machine.DefinitionId+".asset",asset=> {
                    asset.DefinitionId=machine.DefinitionId; asset.DisplayName=machine.Name; asset.EquipmentType=machine.EquipmentType;
                    asset.PurchasePrice=machine.PurchasePrice; asset.RentalPricePerDay=machine.RentalPricePerDay; asset.MaintenanceCost=machine.MaintenanceCost;
                    asset.FuelCapacity=machine.FuelCapacity; asset.FuelPerHour=machine.FuelPerHour; asset.CostPerHour=machine.CostPerHour; asset.ProductivityMultiplier=machine.ProductivityMultiplier;
                },ref created,ref skipped);
                Create<EquipmentAsset>(EquipmentFolder+"/"+machine.Id+".asset",asset=>asset.Data=machine,ref created,ref skipped);
            }
            var phases=SampleRenovationPhaseFactory.CreateHouseRenovationPhases(); new PhaseDependencyGraph(phases);
            foreach(var phase in phases)
                Create<ConstructionPhaseDefinition>(PhaseFolder+"/"+phase.Id+".asset",asset=>asset.Configure(phase),ref created,ref skipped);
            Create<ConstructionSettingsDefinition>("Assets/Data/ConstructionSettings.asset",asset=>asset.CrewEfficiency=new CrewEfficiencyConfiguration(),ref created,ref skipped);
            var settings=AssetDatabase.LoadAssetAtPath<ConstructionSettingsDefinition>("Assets/Data/ConstructionSettings.asset");
            foreach(var guid in AssetDatabase.FindAssets("t:SimulationConfiguration",new[]{"Assets/_Project"}))
            {
                var simulation=AssetDatabase.LoadAssetAtPath<SimulationConfiguration>(AssetDatabase.GUIDToAssetPath(guid));
                if(simulation.ConstructionSettings!=null) continue;
                simulation.ConstructionSettings=settings; EditorUtility.SetDirty(simulation);
            }
            // Validate the actual retained/generated set, including designer edits on reruns.
            var actual=new List<ConstructionPhaseRuntimeData>();
            foreach(var phase in phases) actual.Add(AssetDatabase.LoadAssetAtPath<ConstructionPhaseDefinition>(PhaseFolder+"/"+phase.Id+".asset").CreateRuntimeData());
            new PhaseDependencyGraph(actual);
            AssetDatabase.SaveAssets();
            Selection.objects=new UnityEngine.Object[] {AssetDatabase.LoadAssetAtPath<DefaultAsset>(EmployeeFolder),AssetDatabase.LoadAssetAtPath<DefaultAsset>(EquipmentFolder),AssetDatabase.LoadAssetAtPath<DefaultAsset>(PhaseFolder)};
            Debug.Log($"Workforce generation complete: {created} assets created, {skipped} existing assets skipped. Six employees, two machines, and sixteen wired phases verified. Values are provisional and editable.");
        }
        private static void Create<T>(string path,Action<T> configure,ref int created,ref int skipped) where T:ScriptableObject
        {
            if(File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path)!=null)
            {
                if(AssetDatabase.LoadAssetAtPath<T>(path)==null) throw new InvalidOperationException("Incompatible existing asset at "+path);
                skipped++; return;
            }
            var asset=ScriptableObject.CreateInstance<T>();
            try { configure(asset); AssetDatabase.CreateAsset(asset,path); created++; }
            catch { UnityEngine.Object.DestroyImmediate(asset); throw; }
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
