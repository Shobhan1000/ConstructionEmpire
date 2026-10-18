using System;
using ConstructionEmpire.Data.Save;

namespace ConstructionEmpire.Systems.Persistence
{
    public static class SaveMigrationRunner
    {
        // Version 2 is the foundation's v1 -> v2 migration target.
        // Increase only when every affected DTO has the corresponding migration.
        public const int CurrentSchemaVersion = 2;

        /// <summary>
        /// Migrates one DTO in place. Does not traverse nested DTOs or write files.
        /// On failure, discard the partially migrated graph; never overwrite the source save.
        /// Pass a logger from the host (for example Debug.Log at the Unity boundary).
        /// </summary>
        public static void Run(SaveDataBase data, Action<string> log = null)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.SchemaVersion < 1 || data.SchemaVersion > CurrentSchemaVersion)
                throw new NotSupportedException(
                    $"{data.GetType().Name} schema {data.SchemaVersion} is unsupported; " +
                    $"expected 1 through {CurrentSchemaVersion}.");

            Action<string> writeLog = log ?? Console.WriteLine;
            while (data.SchemaVersion < CurrentSchemaVersion)
            {
                int previousVersion = data.SchemaVersion;
                data.Migrate();
                if (data.SchemaVersion != previousVersion + 1)
                    throw new InvalidOperationException(
                        $"{data.GetType().Name}.Migrate must advance exactly one version " +
                        $"from {previousVersion}; got {data.SchemaVersion}.");

                writeLog($"{data.GetType().Name}: schema {previousVersion} -> {data.SchemaVersion}");
            }
        }
    }
}
