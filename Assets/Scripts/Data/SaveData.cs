using System;
using System.Collections.Generic;

namespace ConstructionEmpire.Data
{
    /// <summary>
    /// Top-level save file contents (spec section 49). Each system owns its
    /// own slice and knows how to capture/restore it - SaveSystem just
    /// wires the calls together. Never serializes Unity objects directly.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int SaveVersion;
        public string SavedAtUtcIso;

        public CompanyData Company;
        public List<MaterialStockEntry> MaterialInventory = new List<MaterialStockEntry>();
        public List<WorkerData> Workers = new List<WorkerData>();
        public List<CrewData> Crews = new List<CrewData>();
        public List<EquipmentRuntimeData> Fleet = new List<EquipmentRuntimeData>();
        public List<ProjectRuntimeData> AvailableOpportunities = new List<ProjectRuntimeData>();
        public List<ProjectRuntimeData> ActiveProjects = new List<ProjectRuntimeData>();
        public List<ProjectRuntimeData> CompletedProjects = new List<ProjectRuntimeData>();
        public List<TransactionRecord> Transactions = new List<TransactionRecord>();
        public List<ClientData> Clients = new List<ClientData>();
    }
}
