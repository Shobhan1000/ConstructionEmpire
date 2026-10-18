using NUnit.Framework;
using ConstructionEmpire.Data;
using ConstructionEmpire.Projects;

namespace ConstructionEmpire.Tests
{
    public class QualityScoringTests
    {
        [Test]
        public void BlendInHouseQuality_WithGoodInputs_ProducesHighScore()
        {
            float score = QualityScoring.BlendInHouseQuality(averageCrewProductivity: 1.2f, equipmentFactor: 1.0f, materialFactor: 1.0f);
            Assert.Greater(score, 0.6f);
        }

        [Test]
        public void BlendInHouseQuality_WithPoorInputs_ProducesLowScore()
        {
            // Mirrors a genuinely under-resourced phase: tired/unskilled
            // crew, no equipment assigned (equipmentFactor pinned at the
            // system's fixed 0.5 "required but missing" value), and
            // materials mostly unordered.
            float score = QualityScoring.BlendInHouseQuality(averageCrewProductivity: 0.3f, equipmentFactor: 0.5f, materialFactor: 0.3f);
            Assert.Less(score, 0.4f);
        }

        [Test]
        public void BlendInHouseQuality_ClampsAboveOne()
        {
            float score = QualityScoring.BlendInHouseQuality(averageCrewProductivity: 5f, equipmentFactor: 5f, materialFactor: 5f);
            Assert.LessOrEqual(score, 1f);
        }

        [Test]
        public void BlendInHouseQuality_ClampsBelowZero()
        {
            float score = QualityScoring.BlendInHouseQuality(averageCrewProductivity: -5f, equipmentFactor: -5f, materialFactor: -5f);
            Assert.GreaterOrEqual(score, 0f);
        }

        [TestCase(0.95f, QualityLevel.Premium)]
        [TestCase(0.8f, QualityLevel.Excellent)]
        [TestCase(0.65f, QualityLevel.Good)]
        [TestCase(0.5f, QualityLevel.Average)]
        [TestCase(0.25f, QualityLevel.BelowAverage)]
        [TestCase(0.05f, QualityLevel.Poor)]
        public void ScoreToLevel_MapsExpectedRanges(float score, QualityLevel expected)
        {
            Assert.AreEqual(expected, QualityScoring.ScoreToLevel(score));
        }
    }
}
