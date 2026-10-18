using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Persistence;
using ConstructionEmpire.Systems.Simulation;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public class SaveGameServiceTests
    {
        private static SaveGameData CreateSave() => new SaveGameData
        {
            Company = new CompanyData("Saved Company", 100000f),
            Clock = new GameTimeData(),
            SimulationSeed = 42
        };

        [Test]
        public void RoundTrip_MigratesEveryDto_AndPreservesCompanyAndClockDebt()
        {
            using (var original = new SimulationManager(new CompanyData("Saved Company", 100000f),
                new GameTimeData { MaxTicksPerCall = 2 }, 42))
            {
                original.Company.ApplyTransaction(-25f, "Supplies");
                original.Advance(5.5d);
                var snapshot = original.CaptureSave();
                var messages = new List<string>();
                string json = SaveGameService.Serialize(snapshot, messages.Add);
                Assert.That(messages.Count, Is.EqualTo(3));
                Assert.That(snapshot.SchemaVersion, Is.EqualTo(2));
                Assert.That(snapshot.Company.SchemaVersion, Is.EqualTo(2));
                Assert.That(snapshot.Clock.SchemaVersion, Is.EqualTo(2));
                using (var restored = SaveGameService.Restore(json, messages.Add))
                {
                    Assert.That(restored.Company.CompanyName, Is.EqualTo("Saved Company"));
                    Assert.That(restored.Company.CashBalance, Is.EqualTo(99975f));
                    Assert.That(restored.Time.TotalHours, Is.EqualTo(2));
                    Assert.That(restored.Time.PendingSimulationSeconds, Is.EqualTo(3.5d));
                    Assert.That(restored.Random.Seed, Is.EqualTo(42));
                    Assert.That(restored.Advance(0d), Is.EqualTo(original.Advance(0d)));
                    Assert.That(restored.Time.TotalHours, Is.EqualTo(original.Time.TotalHours));
                    Assert.That(restored.Time.PendingSimulationSeconds, Is.EqualTo(original.Time.PendingSimulationSeconds));
                }
            }
        }

        [Test]
        public void VersionOneJson_LoadMigratesAllNestedDtos()
        {
            string json = SaveGameService.Serialize(CreateSave(), _ => { });
            json = json.Replace("\"SchemaVersion\":2", "\"SchemaVersion\":1");
            var messages = new List<string>();
            var loaded = SaveGameService.Deserialize(json, messages.Add);
            Assert.That(messages.Count, Is.EqualTo(3));
            Assert.That(loaded.Company.SchemaVersion, Is.EqualTo(2));
            Assert.That(loaded.Clock.SchemaVersion, Is.EqualTo(2));
        }

        [Test]
        public void FutureNestedVersion_IsRejectedBeforeRootMutation()
        {
            var save = CreateSave();
            save.Company.SchemaVersion = SaveMigrationRunner.CurrentSchemaVersion + 1;
            Assert.Throws<NotSupportedException>(() => SaveGameService.MigrateAndValidate(save));
            Assert.That(save.SchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void MissingRequiredData_IsRejected()
        {
            Assert.Throws<SerializationException>(() => SaveGameService.Deserialize("{}"));
            var save = CreateSave();
            save.Company = null;
            Assert.Throws<SerializationException>(() => SaveGameService.MigrateAndValidate(save));
        }

        [Test]
        public void PauseAndCalendarConfiguration_SurviveRoundTrip()
        {
            var save = CreateSave();
            save.Clock.Speed = SimulationSpeed.Pause;
            save.Clock.SecondsPerTick = 0.25d;
            save.Clock.HoursPerDay = 12;
            save.Clock.DaysPerMonth = 30;
            save.Clock.PendingSimulationSeconds = 1.5d;
            var loaded = SaveGameService.Deserialize(SaveGameService.Serialize(save, _ => { }));
            Assert.That(loaded.Clock.Speed, Is.EqualTo(SimulationSpeed.Pause));
            Assert.That(loaded.Clock.SecondsPerTick, Is.EqualTo(0.25d));
            Assert.That(loaded.Clock.HoursPerDay, Is.EqualTo(12));
            Assert.That(loaded.Clock.DaysPerMonth, Is.EqualTo(30));
            Assert.That(loaded.Clock.PendingSimulationSeconds, Is.EqualTo(1.5d));
        }

        [Test]
        public void DiskSave_ReplacesValidSave_AndPreservesItOnValidationFailure()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ConstructionEmpireTest-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            try
            {
                var save = CreateSave();
                SaveGameService.SaveToFile(path, save, _ => { });
                save.Company.ApplyTransaction(-10f, "Supplies");
                SaveGameService.SaveToFile(path, save);
                Assert.That(SaveGameService.LoadFromFile(path).Company.CashBalance, Is.EqualTo(99990f));
                string previous = File.ReadAllText(path);
                save.SchemaVersion = 999;
                Assert.Throws<NotSupportedException>(() => SaveGameService.SaveToFile(path, save));
                Assert.That(File.ReadAllText(path), Is.EqualTo(previous));
            }
            finally
            {
                // Only this test's uniquely created directory is removed.
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
