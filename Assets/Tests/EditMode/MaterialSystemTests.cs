using System;
using NUnit.Framework;
using UnityEngine;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Materials;

namespace ConstructionEmpire.Tests
{
    public class MaterialSystemTests
    {
        private GameObject _go;
        private GameTimeManager _timeManager;
        private SimulationManager _simulation;
        private CompanySystem _company;
        private MaterialSystem _materials;
        private ContentLibrary _content;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestTimeManager");
            _timeManager = _go.AddComponent<GameTimeManager>();
            _timeManager.Initialize(3f, new DateTime(2025, 1, 1));

            _simulation = new SimulationManager();
            _company = new CompanySystem();
            _company.FoundCompany("Test Co.", 250000, 50f, _timeManager.CurrentDate);

            _content = DefaultContentFactory.BuildDefaultLibrary();
            _materials = new MaterialSystem(_content, _company, _simulation);
        }

        [TearDown]
        public void TearDown()
        {
            _simulation.Dispose();
            UnityEngine.Object.DestroyImmediate(_go);
            EventBus.Clear();
        }

        [Test]
        public void OrderMaterial_SpendsCashImmediatelyAndIncreasesOnOrder()
        {
            double cashBefore = _company.Data.Cash;

            bool ordered = _materials.OrderMaterial(MaterialType.Concrete, 50, "test", _timeManager.CurrentDate);

            Assert.IsTrue(ordered);
            Assert.Less(_company.Data.Cash, cashBefore);
            Assert.AreEqual(50, _materials.GetOnOrder(MaterialType.Concrete));
            Assert.AreEqual(0, _materials.GetOnHand(MaterialType.Concrete));
        }

        [Test]
        public void OrderMaterial_AfterLeadTimeElapses_DeliversToOnHand()
        {
            var def = _content.GetMaterial(MaterialType.Concrete);
            _materials.OrderMaterial(MaterialType.Concrete, 50, "test", _timeManager.CurrentDate);

            for (int i = 0; i < def.DefaultLeadTimeDays; i++)
            {
                _timeManager.AdvanceOneDayImmediate();
            }

            Assert.AreEqual(50, _materials.GetOnHand(MaterialType.Concrete));
            Assert.AreEqual(0, _materials.GetOnOrder(MaterialType.Concrete));
        }

        [Test]
        public void ConsumeMaterial_WithInsufficientStock_ReturnsFalseAndDoesNotGoNegative()
        {
            bool consumed = _materials.ConsumeMaterial(MaterialType.Steel, 10);
            Assert.IsFalse(consumed);
            Assert.AreEqual(0, _materials.GetOnHand(MaterialType.Steel));
        }
    }
}
