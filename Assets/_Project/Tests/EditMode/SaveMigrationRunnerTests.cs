using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Systems.Persistence;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public class SaveMigrationRunnerTests
    {
        private sealed class DummySaveData : SaveDataBase
        {
            public int MigrationCalls;
            public override void Migrate()
            {
                MigrationCalls++;
                SchemaVersion++;
            }
        }

        private sealed class BrokenSaveData : SaveDataBase
        {
            public int Step;
            public override void Migrate() => SchemaVersion += Step;
        }

        private sealed class UnimplementedSaveData : SaveDataBase { }

        [Test]
        public void Run_MigratesVersionOneToTwo_AndLogsTheStep()
        {
            var data = new DummySaveData();
            var messages = new List<string>();
            Assert.That(data.SchemaVersion, Is.EqualTo(1));

            SaveMigrationRunner.Run(data, messages.Add);

            Assert.That(data.SchemaVersion, Is.EqualTo(2));
            Assert.That(data.SchemaVersion, Is.EqualTo(SaveMigrationRunner.CurrentSchemaVersion));
            Assert.That(data.MigrationCalls, Is.EqualTo(1));
            Assert.That(messages, Is.EqualTo(new[] { "DummySaveData: schema 1 -> 2" }));
        }

        [Test]
        public void Run_CurrentData_IsUnchangedAndDoesNotLog()
        {
            var data = new DummySaveData { SchemaVersion = SaveMigrationRunner.CurrentSchemaVersion };
            var messages = new List<string>();
            SaveMigrationRunner.Run(data, messages.Add);
            Assert.That(data.MigrationCalls, Is.Zero);
            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void Run_Null_Throws() =>
            Assert.Throws<ArgumentNullException>(() => SaveMigrationRunner.Run(null));

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(SaveMigrationRunner.CurrentSchemaVersion + 1)]
        public void Run_UnsupportedVersion_ThrowsWithoutMigrating(int version)
        {
            var data = new DummySaveData { SchemaVersion = version };
            Assert.Throws<NotSupportedException>(() => SaveMigrationRunner.Run(data));
            Assert.That(data.MigrationCalls, Is.Zero);
            Assert.That(data.SchemaVersion, Is.EqualTo(version));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(2)]
        public void Run_InvalidMigrationStep_ThrowsWithoutLoggingSuccess(int step)
        {
            var data = new BrokenSaveData { Step = step };
            var messages = new List<string>();
            Assert.Throws<InvalidOperationException>(() => SaveMigrationRunner.Run(data, messages.Add));
            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void Run_MissingMigration_Throws() =>
            Assert.Throws<NotSupportedException>(() => SaveMigrationRunner.Run(new UnimplementedSaveData()));
    }
}
