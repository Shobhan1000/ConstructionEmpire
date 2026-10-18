using System;
using System.IO;
using UnityEngine;
using ConstructionEmpire.Clients;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Equipment;
using ConstructionEmpire.Finance;
using ConstructionEmpire.Materials;
using ConstructionEmpire.Projects;
using ConstructionEmpire.Workforce;

namespace ConstructionEmpire.Persistence
{
    /// <summary>
    /// Versioned JSON save/load (spec section 49). Every system owns and
    /// captures/restores its own slice of state - this just sequences the
    /// calls and writes/reads the combined file. Bump CurrentSaveVersion
    /// whenever the shape of SaveData changes; old files still load (with
    /// a warning) since JsonUtility leaves fields absent from the file at
    /// their default value rather than failing outright.
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentSaveVersion = 3;

        private static string GetSavePath(string slotName)
        {
            string fileName = "save_" + slotName + ".json";
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        public static bool SaveGame(
            string slotName,
            CompanySystem companySystem,
            MaterialSystem materialSystem,
            ConstructionProjectSystem projectSystem,
            CrewSystem crewSystem,
            EquipmentSystem equipmentSystem,
            FinanceSystem financeSystem,
            ClientSystem clientSystem)
        {
            if (companySystem == null)
            {
                Debug.LogError("[SaveSystem] Cannot save: CompanySystem is null.");
                return false;
            }

            try
            {
                projectSystem.CaptureState(out var opportunities, out var active, out var completed);

                var saveData = new SaveData
                {
                    SaveVersion = CurrentSaveVersion,
                    SavedAtUtcIso = DateTime.UtcNow.ToString("o"),
                    Company = companySystem.CaptureState(),
                    MaterialInventory = materialSystem.CaptureState(),
                    Workers = crewSystem.CaptureWorkers(),
                    Crews = crewSystem.CaptureCrews(),
                    Fleet = equipmentSystem.CaptureState(),
                    AvailableOpportunities = opportunities,
                    ActiveProjects = active,
                    CompletedProjects = completed,
                    Transactions = financeSystem.CaptureState(),
                    Clients = clientSystem != null ? clientSystem.CaptureState() : new System.Collections.Generic.List<ClientData>()
                };

                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(GetSavePath(slotName), json);

                EventBus.Publish(new GameSavedEvent(slotName));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SaveSystem] Save failed: " + ex.Message);
                return false;
            }
        }

        public static bool LoadGame(
            string slotName,
            CompanySystem companySystem,
            MaterialSystem materialSystem,
            ConstructionProjectSystem projectSystem,
            CrewSystem crewSystem,
            EquipmentSystem equipmentSystem,
            FinanceSystem financeSystem,
            ClientSystem clientSystem)
        {
            if (companySystem == null)
            {
                Debug.LogError("[SaveSystem] Cannot load: CompanySystem is null.");
                return false;
            }

            string path = GetSavePath(slotName);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                var saveData = JsonUtility.FromJson<SaveData>(json);

                if (saveData == null || saveData.Company == null)
                {
                    Debug.LogError("[SaveSystem] Save file is empty or corrupt.");
                    return false;
                }

                if (saveData.SaveVersion != CurrentSaveVersion)
                {
                    Debug.LogWarning("[SaveSystem] Save version mismatch (file: " + saveData.SaveVersion +
                                     ", current: " + CurrentSaveVersion + "). Loading anyway - add migration logic here once fields change between versions.");
                }

                companySystem.RestoreState(saveData.Company);
                materialSystem.RestoreState(saveData.MaterialInventory);
                crewSystem.RestoreState(saveData.Workers, saveData.Crews);
                equipmentSystem.RestoreState(saveData.Fleet);
                projectSystem.RestoreState(saveData.AvailableOpportunities, saveData.ActiveProjects, saveData.CompletedProjects);
                financeSystem.RestoreState(saveData.Transactions);
                clientSystem?.RestoreState(saveData.Clients);

                EventBus.Publish(new GameLoadedEvent(slotName));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SaveSystem] Load failed: " + ex.Message);
                return false;
            }
        }

        public static bool SaveExists(string slotName) => File.Exists(GetSavePath(slotName));

        public static bool DeleteSave(string slotName)
        {
            string path = GetSavePath(slotName);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }
}
