using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Subcontractors;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Tests
{
    public class SubcontractorSystemTests
    {
        private ContentLibrary _content;
        private CompanySystem _company;
        private SimulationManager _simulation;
        private SubcontractorSystem _subcontractors;
        private DateTime _date;

        [SetUp]
        public void SetUp()
        {
            _date = new DateTime(2025, 1, 1);
            _content = DefaultContentFactory.BuildDefaultLibrary();
            _company = new CompanySystem();
            _company.FoundCompany("Test Co.", 250000, 50f, _date);
            _simulation = new SimulationManager();
            _subcontractors = new SubcontractorSystem(_content, _company, _simulation, new SeededRandom(3));
        }

        [TearDown]
        public void TearDown()
        {
            _simulation.Dispose();
            EventBus.Clear();
        }

        [Test]
        public void GetQuotes_ForElectricalSpecialty_ReturnsBothElectricians()
        {
            var phaseDef = _content.GetPhase("electrical_rough");
            var quotes = _subcontractors.GetQuotes(SubcontractorSpecialty.Electrical, phaseDef);

            Assert.AreEqual(2, quotes.Count);
            Assert.IsTrue(quotes.All(q => q.Price > 0 && q.EstimatedDurationDays > 0));
        }

        [Test]
        public void GetQuotes_ForNoneSpecialty_ReturnsEmpty()
        {
            var phaseDef = _content.GetPhase("site_prep");
            var quotes = _subcontractors.GetQuotes(SubcontractorSpecialty.None, phaseDef);

            Assert.AreEqual(0, quotes.Count);
        }

        [Test]
        public void AwardSubcontract_WithInsufficientFunds_ReturnsFalseAndSchedulesNothing()
        {
            var poorCompany = new CompanySystem();
            poorCompany.FoundCompany("Broke Co.", 10, 50f, _date);
            var poorSubcontractors = new SubcontractorSystem(_content, poorCompany, _simulation, new SeededRandom(4));

            var phaseDef = _content.GetPhase("electrical_rough");
            bool awarded = poorSubcontractors.AwardSubcontract("sub_electrical_bright_spark", phaseDef, _date, "Test Project", onTime => { });

            Assert.IsFalse(awarded);
            Assert.AreEqual(10, poorCompany.Data.Cash);
        }

        [Test]
        public void AwardSubcontract_ChargesCashAndEventuallyInvokesCallback()
        {
            var phaseDef = _content.GetPhase("electrical_rough");
            double cashBefore = _company.Data.Cash;
            bool callbackFired = false;

            bool awarded = _subcontractors.AwardSubcontract("sub_electrical_bright_spark", phaseDef, _date, "Test Project", onTime => callbackFired = true);

            Assert.IsTrue(awarded);
            Assert.Less(_company.Data.Cash, cashBefore);
            Assert.IsFalse(callbackFired); // hasn't resolved yet - nothing has advanced the clock

            // Advance far enough that even a maximally delayed outcome
            // would have resolved by now.
            for (int day = 1; day <= 30; day++)
            {
                EventBus.Publish(new HourAdvancedEvent(_date.AddDays(day)));
            }

            Assert.IsTrue(callbackFired);
        }
    }
}
