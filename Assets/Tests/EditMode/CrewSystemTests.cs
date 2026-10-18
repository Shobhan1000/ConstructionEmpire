using NUnit.Framework;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Workforce;

namespace ConstructionEmpire.Tests
{
    public class CrewSystemTests
    {
        private ContentLibrary _content;
        private CompanySystem _company;
        private CrewSystem _crews;

        [SetUp]
        public void SetUp()
        {
            _content = DefaultContentFactory.BuildDefaultLibrary();
            _company = new CompanySystem();
            _company.FoundCompany("Test Co.", 250000, 50f, System.DateTime.Now);
            _crews = new CrewSystem(_content, _company);
        }

        [TearDown]
        public void TearDown()
        {
            _crews.Dispose();
            EventBus.Clear();
        }

        [Test]
        public void HireWorker_SetsProductivityAndWageFromContent()
        {
            var worker = _crews.HireWorker(WorkerType.Carpenter, "Alex");
            var def = _content.GetWorkerType(WorkerType.Carpenter);

            Assert.AreEqual(def.BaseProductivity, worker.Productivity);
            Assert.AreEqual(def.BaseDailyWage, worker.DailyWage);
        }

        [Test]
        public void CreateCrew_WithOneNormalWorker_HasProductivityNearOne()
        {
            var worker = _crews.HireWorker(WorkerType.GeneralLaborer, "Sam");
            var crew = _crews.CreateCrew("Crew A", new[] { worker.Id });

            float productivity = _crews.GetCrewProductivity(crew.Id);

            Assert.Greater(productivity, 0.8f);
            Assert.Less(productivity, 1.3f);
        }

        [Test]
        public void OnDayAdvanced_WorkingCrew_IncreasesWorkerFatigueAndChargesWages()
        {
            var worker = _crews.HireWorker(WorkerType.GeneralLaborer, "Sam");
            var crew = _crews.CreateCrew("Crew A", new[] { worker.Id });
            _crews.AssignCrewToProject(crew.Id, "some-project", "some-phase");

            double cashBefore = _company.Data.Cash;
            EventBus.Publish(new DayAdvancedEvent(System.DateTime.Now));

            Assert.Greater(worker.Fatigue, 0f);
            Assert.Less(_company.Data.Cash, cashBefore);
        }

        [Test]
        public void OnDayAdvanced_IdleWorker_FatigueDecreases()
        {
            var worker = _crews.HireWorker(WorkerType.GeneralLaborer, "Sam");
            worker.Fatigue = 50f;

            EventBus.Publish(new DayAdvancedEvent(System.DateTime.Now));

            Assert.Less(worker.Fatigue, 50f);
        }
    }
}
