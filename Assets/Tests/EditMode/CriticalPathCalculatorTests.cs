using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Content;
using ConstructionEmpire.Data;
using ConstructionEmpire.Projects;

namespace ConstructionEmpire.Tests
{
    public class CriticalPathCalculatorTests
    {
        private ContentLibrary _content;

        [SetUp]
        public void SetUp()
        {
            _content = DefaultContentFactory.BuildDefaultLibrary();
        }

        [Test]
        public void Calculate_LastPhaseInChain_IsAlwaysCritical()
        {
            var template = _content.GetProjectTemplate(ProjectType.HouseRenovation);
            var schedule = CriticalPathCalculator.Calculate(_content, template.PhaseIds);

            var finalInspection = schedule.First(s => s.PhaseId == "final_inspection");

            Assert.IsTrue(finalInspection.IsCritical);
            Assert.AreEqual(0, finalInspection.Slack);
        }

        [Test]
        public void Calculate_ProjectDuration_MatchesLongestChain()
        {
            var template = _content.GetProjectTemplate(ProjectType.HouseRenovation);
            var schedule = CriticalPathCalculator.Calculate(_content, template.PhaseIds);

            // site_prep(4) + foundation(6) + framing(8) + electrical_rough or
            // plumbing_rough(4) + insulation(3) + drywall(5) + flooring(4) +
            // interior_finish(6) + final_inspection(1) = 41 days along the
            // longest path (roofing is a shorter parallel branch).
            var finalInspection = schedule.First(s => s.PhaseId == "final_inspection");
            Assert.AreEqual(41, finalInspection.EarliestFinish);
        }

        [Test]
        public void Calculate_PhasesOnCriticalPath_HaveZeroSlack()
        {
            var template = _content.GetProjectTemplate(ProjectType.HouseRenovation);
            var schedule = CriticalPathCalculator.Calculate(_content, template.PhaseIds);

            foreach (var phaseId in new[] { "site_prep", "foundation", "framing" })
            {
                var info = schedule.First(s => s.PhaseId == phaseId);
                Assert.AreEqual(0, info.Slack, phaseId + " should be on the critical path");
            }
        }

        [Test]
        public void Calculate_EmptyPhaseList_ReturnsEmptySchedule()
        {
            var schedule = CriticalPathCalculator.Calculate(_content, new string[0]);
            Assert.AreEqual(0, schedule.Count);
        }
    }
}
