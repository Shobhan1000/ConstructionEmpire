using System;
using NUnit.Framework;
using UnityEngine;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Equipment;
using ConstructionEmpire.Materials;
using ConstructionEmpire.Projects;
using ConstructionEmpire.Safety;
using ConstructionEmpire.Subcontractors;
using ConstructionEmpire.Utilities;
using ConstructionEmpire.Weather;
using ConstructionEmpire.Workforce;

namespace ConstructionEmpire.Tests
{
    public class SafetyIncidentSystemTests
    {
        private GameObject _go;
        private GameTimeManager _timeManager;
        private ContentLibrary _content;
        private CompanySystem _company;
        private SimulationManager _simulation;
        private MaterialSystem _materials;
        private CrewSystem _crews;
        private EquipmentSystem _equipment;
        private SubcontractorSystem _subcontractors;
        private WeatherSystem _weather;
        private ConstructionProjectSystem _projects;
        private SafetyIncidentSystem _safety;
        private DateTime _date;

        [SetUp]
        public void SetUp()
        {
            _date = new DateTime(2025, 1, 1);
            _go = new GameObject("TestTimeManager");
            _timeManager = _go.AddComponent<GameTimeManager>();
            _timeManager.Initialize(3f, _date);

            _content = DefaultContentFactory.BuildDefaultLibrary();
            _company = new CompanySystem();
            _company.FoundCompany("Test Co.", 1000000, 50f, _date);
            _simulation = new SimulationManager();
            _materials = new MaterialSystem(_content, _company, _simulation);
            _crews = new CrewSystem(_content, _company);
            _equipment = new EquipmentSystem(_content, _company, _simulation, new SeededRandom(1));
            _subcontractors = new SubcontractorSystem(_content, _company, _simulation, new SeededRandom(2));
            _weather = new WeatherSystem(new SeededRandom(3));
            _projects = new ConstructionProjectSystem(_content, _company, _materials, _crews, _equipment, _subcontractors, _weather, _timeManager);
            _safety = new SafetyIncidentSystem(_projects, _crews, _company, new SeededRandom(99));
        }

        [TearDown]
        public void TearDown()
        {
            _safety.Dispose();
            _projects.Dispose();
            _crews.Dispose();
            _equipment.Dispose();
            _weather.Dispose();
            _simulation.Dispose();
            UnityEngine.Object.DestroyImmediate(_go);
            EventBus.Clear();
        }

        [Test]
        public void NoActiveProjects_DayTicksProduceNoIncidents()
        {
            double cashBefore = _company.Data.Cash;

            for (int i = 0; i < 50; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(_date.AddDays(i)));
            }

            Assert.AreEqual(cashBefore, _company.Data.Cash);
        }

        [Test]
        public void HighFatigueCrewedPhase_EventuallyTriggersAnIncident()
        {
            var project = new ProjectRuntimeData
            {
                Id = "proj-safety",
                Type = ProjectType.OfficeRenovation,
                DisplayName = "Safety Test Project",
                ContractValue = 100000,
                DeadlineIso = _date.AddDays(300).ToString("o"),
                Status = ProjectStatus.Available,
                Phases = { new PhaseRuntimeData { PhaseId = "electrical_rough", DisplayName = "Electrical (Rough-in)" } }
            };
            _projects.StartProject(project, _date);

            var worker = _crews.HireWorker(WorkerType.Electrician, "Pat");
            worker.Fatigue = 100f; // maximize incident chance for a fast, reliable test
            var crew = _crews.CreateCrew("Crew", new[] { worker.Id });
            _projects.AssignCrewToPhase(project.Id, "electrical_rough", crew.Id);

            double cashBefore = _company.Data.Cash;
            float reputationBefore = _company.Data.Reputation;
            bool incidentObserved = false;

            for (int i = 0; i < 150 && !incidentObserved; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(_date.AddDays(i + 1)));
                if (_company.Data.Cash < cashBefore || _company.Data.Reputation < reputationBefore)
                {
                    incidentObserved = true;
                }
            }

            Assert.IsTrue(incidentObserved, "Expected at least one safety incident over 150 days at maximum fatigue.");
        }
    }
}
