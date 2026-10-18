using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Systems.Simulation;

namespace ConstructionEmpire.Systems.Persistence
{
    public static class SaveGameService
    {
        public static void MigrateAndValidate(SaveGameData data, Action<string> log = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            // Reject unsupported nested schemas before mutating the graph.
            CheckVersion(data);
            if (data.Company == null || data.Clock == null)
                throw new SerializationException("Save must contain company and clock data.");
            CheckVersion(data.Company);
            CheckVersion(data.Clock);
            SaveMigrationRunner.Run(data, log);
            SaveMigrationRunner.Run(data.Company, log);
            SaveMigrationRunner.Run(data.Clock, log);
            data.Validate();
        }

        private static void CheckVersion(SaveDataBase data)
        {
            if (data.SchemaVersion < 1 || data.SchemaVersion > SaveMigrationRunner.CurrentSchemaVersion)
                throw new NotSupportedException($"Unsupported {data.GetType().Name} schema {data.SchemaVersion}.");
        }

        public static string Serialize(SaveGameData data, Action<string> log = null)
        {
            MigrateAndValidate(data, log);
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(SaveGameData)).WriteObject(stream, data);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static SaveGameData Deserialize(string json, Action<string> log = null)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Save JSON is empty.", nameof(json));
            SaveGameData data;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                data = (SaveGameData)new DataContractJsonSerializer(typeof(SaveGameData)).ReadObject(stream);
            MigrateAndValidate(data, log);
            return data;
        }

        public static SimulationManager Restore(string json, Action<string> log = null)
        {
            SaveGameData data = Deserialize(json, log);
            return new SimulationManager(data.Company, data.Clock, data.SimulationSeed);
        }

        public static void SaveToFile(string path, SaveGameData data, Action<string> log = null)
        {
            // Validate and serialize before touching a previous save.
            string json = Serialize(data, log);
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                // Same-directory replacement preserves the original if replacement fails.
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, null);
                else File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public static SaveGameData LoadFromFile(string path, Action<string> log = null) =>
            Deserialize(File.ReadAllText(path, Encoding.UTF8), log);
    }
}
