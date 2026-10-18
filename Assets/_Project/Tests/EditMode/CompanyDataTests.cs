using System;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Simulation.Events;
using ConstructionEmpire.Data.Company;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public class CompanyDataTests
    {
        [SetUp] public void SetUp() => GameEvents.Clear();
        [TearDown] public void TearDown() => GameEvents.Clear();
        [Test]
        public void Overdraft_IsExplicitlyRejectedWithoutBalanceMutation()
        {
            var company = new CompanyData("Test Company", 100f);
            int changed = 0, rejected = 0;
            GameEvents.Subscribe<BalanceChangedEvent>(payload => changed++);
            GameEvents.Subscribe<TransactionRejectedEvent>(payload =>
            {
                rejected++;
                Assert.That(payload.Amount, Is.EqualTo(-101f));
                Assert.That(payload.Reason, Is.EqualTo("Purchase"));
            });
            Assert.That(company.ApplyTransaction(-101f, "Purchase"), Is.EqualTo(TransactionResult.InsufficientFunds));
            Assert.That(company.CashBalance, Is.EqualTo(100f));
            Assert.That(changed, Is.Zero);
            Assert.That(rejected, Is.EqualTo(1));
        }

        [Test]
        public void Transactions_AllowExactZeroAndPublishBalanceDetails()
        {
            var company = new CompanyData("Test Company", 100f);
            float oldBalance = -1f, newBalance = -1f;
            string eventReason = null;
            GameEvents.Subscribe<BalanceChangedEvent>(payload =>
            { oldBalance = payload.PreviousBalance; newBalance = payload.CurrentBalance; eventReason = payload.Reason;
              Assert.That(payload.Company, Is.SameAs(company)); });
            Assert.That(company.ApplyTransaction(-100f, "Payment"), Is.EqualTo(TransactionResult.Applied));
            Assert.That(oldBalance, Is.EqualTo(100f));
            Assert.That(newBalance, Is.Zero);
            Assert.That(eventReason, Is.EqualTo("Payment"));
            company.ApplyTransaction(50f, "Income");
            Assert.That(company.CashBalance, Is.EqualTo(50f));
            Assert.That(company.Reputation, Is.EqualTo(50f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteTransactions_AreRejected(float amount)
        {
            var company = new CompanyData("Test", 100f);
            Assert.Throws<ArgumentOutOfRangeException>(() => company.ApplyTransaction(amount, "Invalid"));
            Assert.That(company.CashBalance, Is.EqualTo(100f));
        }
    }
}
