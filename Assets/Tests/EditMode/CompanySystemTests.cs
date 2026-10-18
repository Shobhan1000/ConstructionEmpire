using System;
using NUnit.Framework;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Core;

namespace ConstructionEmpire.Tests
{
    public class CompanySystemTests
    {
        private CompanySystem _company;

        [SetUp]
        public void SetUp()
        {
            _company = new CompanySystem();
            _company.FoundCompany("Test Co.", 250000, 50f, new DateTime(2025, 1, 1));
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        [Test]
        public void FoundCompany_SetsStartingValues()
        {
            Assert.AreEqual("Test Co.", _company.Data.CompanyName);
            Assert.AreEqual(250000, _company.Data.Cash);
            Assert.AreEqual(50f, _company.Data.Reputation);
        }

        [Test]
        public void AddCash_IncreasesBalance()
        {
            _company.AddCash(10000, "Test income");
            Assert.AreEqual(260000, _company.Data.Cash);
        }

        [Test]
        public void TrySpendCash_WithSufficientFunds_Succeeds()
        {
            bool result = _company.TrySpendCash(50000, "Test expense");
            Assert.IsTrue(result);
            Assert.AreEqual(200000, _company.Data.Cash);
        }

        [Test]
        public void TrySpendCash_WithInsufficientFunds_FailsAndDoesNotChangeBalance()
        {
            bool result = _company.TrySpendCash(999999999, "Too expensive");
            Assert.IsFalse(result);
            Assert.AreEqual(250000, _company.Data.Cash);
        }

        [Test]
        public void AdjustReputation_ClampsAtUpperBound()
        {
            _company.AdjustReputation(1000f, "Great year");
            Assert.AreEqual(100f, _company.Data.Reputation);
        }

        [Test]
        public void AdjustReputation_ClampsAtLowerBound()
        {
            _company.AdjustReputation(-1000f, "Disaster");
            Assert.AreEqual(0f, _company.Data.Reputation);
        }

        [Test]
        public void CaptureAndRestoreState_RoundTripsCorrectly()
        {
            var captured = _company.CaptureState();
            var restoredCompany = new CompanySystem();
            restoredCompany.RestoreState(captured);

            Assert.AreEqual(_company.Data.CompanyName, restoredCompany.Data.CompanyName);
            Assert.AreEqual(_company.Data.Cash, restoredCompany.Data.Cash);
        }
    }
}
