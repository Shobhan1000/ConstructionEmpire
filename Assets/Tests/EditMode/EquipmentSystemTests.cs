using System;
using NUnit.Framework;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Equipment;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Tests
{
    public class EquipmentSystemTests
    {
        private ContentLibrary _content;
        private CompanySystem _company;
        private SimulationManager _simulation;
        private EquipmentSystem _equipment;

        [SetUp]
        public void SetUp()
        {
            _content = DefaultContentFactory.BuildDefaultLibrary();
            _company = new CompanySystem();
            _company.FoundCompany("Test Co.", 1000000, 50f, new DateTime(2025, 1, 1));
            _simulation = new SimulationManager();
            _equipment = new EquipmentSystem(_content, _company, _simulation, new SeededRandom(42));
        }

        [TearDown]
        public void TearDown()
        {
            _equipment.Dispose();
            _simulation.Dispose();
            EventBus.Clear();
        }

        [Test]
        public void PurchaseEquipment_SpendsCashAndAddsToFleet()
        {
            double cashBefore = _company.Data.Cash;
            var instance = _equipment.PurchaseEquipment(EquipmentType.Excavator);

            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.IsOwned);
            Assert.Less(_company.Data.Cash, cashBefore);
            Assert.AreEqual(1, _equipment.Fleet.Count);
        }

        [Test]
        public void PurchaseEquipment_WithInsufficientFunds_ReturnsNull()
        {
            var poorCompany = new CompanySystem();
            poorCompany.FoundCompany("Broke Co.", 100, 50f, new DateTime(2025, 1, 1));
            var poorEquipment = new EquipmentSystem(_content, poorCompany, _simulation, new SeededRandom(1));

            var instance = poorEquipment.PurchaseEquipment(EquipmentType.Crane);

            Assert.IsNull(instance);
            poorEquipment.Dispose();
        }

        [Test]
        public void AssignEquipmentToProject_SetsStatusToWorking()
        {
            var instance = _equipment.PurchaseEquipment(EquipmentType.Forklift);
            _equipment.AssignEquipmentToProject(instance.Id, "project-1");

            Assert.AreEqual(EquipmentStatus.Working, instance.Status);
        }

        [Test]
        public void WorkingEquipment_DegradesConditionEachDay()
        {
            var instance = _equipment.PurchaseEquipment(EquipmentType.Forklift);
            _equipment.AssignEquipmentToProject(instance.Id, "project-1");

            float before = instance.Condition;
            EventBus.Publish(new DayAdvancedEvent(new DateTime(2025, 1, 2)));

            Assert.Less(instance.Condition, before);
        }

        [Test]
        public void GetEfficiency_ForMissingEquipment_ReturnsZero()
        {
            Assert.AreEqual(0f, _equipment.GetEfficiency("does-not-exist"));
        }
    }
}
