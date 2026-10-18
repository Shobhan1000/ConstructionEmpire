using System.Collections.Generic;
using NUnit.Framework;
using ConstructionEmpire.Bidding;
using ConstructionEmpire.Content;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Tests
{
    public class EstimationSystemTests
    {
        private ContentLibrary _content;
        private EstimationSystem _estimation;

        [SetUp]
        public void SetUp()
        {
            _content = DefaultContentFactory.BuildDefaultLibrary();
            _estimation = new EstimationSystem(_content);
        }

        [Test]
        public void Estimate_SinglePhaseNoMaterialsOrEquipment_MatchesLaborCostPlusOverhead()
        {
            var template = _content.GetProjectTemplate(ProjectType.OfficeRenovation);
            var phaseIds = new List<string> { "final_inspection" };

            var estimate = _estimation.Estimate(template, phaseIds, contingencyPercent: 0f, desiredProfitPercent: 0f);

            var phaseDef = _content.GetPhase("final_inspection");
            Assert.AreEqual(phaseDef.BaseLaborCost, estimate.LaborCost);
            Assert.AreEqual(0, estimate.MaterialCost);
            Assert.AreEqual(0, estimate.EquipmentCost);
            Assert.Greater(estimate.Overhead, 0);
        }

        [Test]
        public void Estimate_IncludesMaterialAndEquipmentCosts()
        {
            var template = _content.GetProjectTemplate(ProjectType.HouseRenovation);
            var phaseIds = new List<string> { "foundation" };

            var estimate = _estimation.Estimate(template, phaseIds, 0f, 0f);

            Assert.Greater(estimate.MaterialCost, 0);
            Assert.Greater(estimate.EquipmentCost, 0);
        }

        [Test]
        public void Estimate_HigherContingencyAndProfit_ProduceHigherRecommendedBid()
        {
            var template = _content.GetProjectTemplate(ProjectType.HouseRenovation);
            var phaseIds = new List<string> { "foundation", "framing" };

            var lowMargin = _estimation.Estimate(template, phaseIds, 0.05f, 0.05f);
            var highMargin = _estimation.Estimate(template, phaseIds, 0.25f, 0.30f);

            Assert.Greater(highMargin.RecommendedBidPrice, lowMargin.RecommendedBidPrice);
        }
    }
}
