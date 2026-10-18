using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ConstructionEmpire.Clients;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Equipment;
using ConstructionEmpire.Materials;
using ConstructionEmpire.Projects;
using ConstructionEmpire.Subcontractors;
using ConstructionEmpire.Utilities;
using ConstructionEmpire.Weather;
using ConstructionEmpire.Workforce;

namespace ConstructionEmpire.Tests
{
    public class ConstructionProjectSystemTests
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
            _company.FoundCompany("Test Co.", 250000, 50f, _date);
            _simulation = new SimulationManager();
            _materials = new MaterialSystem(_content, _company, _simulation);
            _crews = new CrewSystem(_content, _company);
            _equipment = new EquipmentSystem(_content, _company, _simulation, new SeededRandom(7));
            _subcontractors = new SubcontractorSystem(_content, _company, _simulation, new SeededRandom(11));
            _weather = new WeatherSystem(new SeededRandom(13));
            _projects = new ConstructionProjectSystem(_content, _company, _materials, _crews, _equipment, _subcontractors, _weather, _timeManager);
        }

        [TearDown]
        public void TearDown()
        {
            _projects.Dispose();
            _crews.Dispose();
            _equipment.Dispose();
            _weather.Dispose();
            _simulation.Dispose();
            UnityEngine.Object.DestroyImmediate(_go);
            EventBus.Clear();
        }

        /// <summary>A minimal two-phase project (site_prep -> foundation) with no other phases, to isolate dependency gating and progress in one test project.</summary>
        private ProjectRuntimeData MakeTwoPhaseProject()
        {
            return new ProjectRuntimeData
            {
                Id = "proj-1",
                Type = ProjectType.HouseRenovation,
                DisplayName = "Test Project",
                ContractValue = 100000,
                DeadlineIso = _date.AddDays(60).ToString("o"),
                Status = ProjectStatus.Available,
                Phases =
                {
                    new PhaseRuntimeData { PhaseId = "site_prep", DisplayName = "Site Preparation" },
                    new PhaseRuntimeData { PhaseId = "foundation", DisplayName = "Foundation" }
                }
            };
        }

        [Test]
        public void StartProject_UnlocksFirstPhaseOnlyAndPaysUpfront()
        {
            var project = MakeTwoPhaseProject();
            double cashBefore = _company.Data.Cash;

            _projects.StartProject(project, _date);

            var sitePrep = project.Phases.First(p => p.PhaseId == "site_prep");
            var foundation = project.Phases.First(p => p.PhaseId == "foundation");

            Assert.AreEqual(PhaseStatus.Ready, sitePrep.Status);
            Assert.AreEqual(PhaseStatus.Locked, foundation.Status); // gated behind site_prep
            Assert.Greater(_company.Data.Cash, cashBefore);
            Assert.IsTrue(project.UpfrontPaid);
        }

        [Test]
        public void AssignCrewToReadyPhase_MovesItToInProgress()
        {
            var project = MakeTwoPhaseProject();
            _projects.StartProject(project, _date);

            var worker = _crews.HireWorker(WorkerType.GeneralLaborer, "Sam");
            var crew = _crews.CreateCrew("Crew A", new[] { worker.Id });

            bool assigned = _projects.AssignCrewToPhase(project.Id, "site_prep", crew.Id);
            var sitePrep = project.Phases.First(p => p.PhaseId == "site_prep");

            Assert.IsTrue(assigned);
            Assert.AreEqual(PhaseStatus.InProgress, sitePrep.Status);
        }

        [Test]
        public void PhaseWithNoCrewAssigned_MakesNoProgressOnDayTick()
        {
            var project = MakeTwoPhaseProject();
            _projects.StartProject(project, _date);

            var sitePrep = project.Phases.First(p => p.PhaseId == "site_prep");
            sitePrep.Status = PhaseStatus.InProgress; // force in-progress with no crew, to isolate the "idle" case

            EventBus.Publish(new DayAdvancedEvent(_date.AddDays(1)));

            Assert.AreEqual(0f, sitePrep.Progress);
        }

        [Test]
        public void CompletingFirstPhase_UnlocksDependentPhase()
        {
            var project = MakeTwoPhaseProject();
            _projects.StartProject(project, _date);

            var worker = _crews.HireWorker(WorkerType.GeneralLaborer, "Sam");
            var crew = _crews.CreateCrew("Crew A", new[] { worker.Id });
            _projects.AssignCrewToPhase(project.Id, "site_prep", crew.Id);

            // site_prep requires equipment we never assign (halves progress)
            // and weather can also slow outdoor work - loop a generous
            // number of days rather than assuming full-speed completion in
            // exactly BaseDurationDays.
            const int generousDayCount = 60;
            for (int i = 0; i < generousDayCount; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(_date.AddDays(i + 1)));
            }

            var sitePrep = project.Phases.First(p => p.PhaseId == "site_prep");
            var foundation = project.Phases.First(p => p.PhaseId == "foundation");

            Assert.AreEqual(PhaseStatus.Completed, sitePrep.Status);
            Assert.AreEqual(PhaseStatus.Ready, foundation.Status);
        }

        [Test]
        public void CompletingAllPhases_PaysRemainingBalanceAndRaisesReputation()
        {
            var project = new ProjectRuntimeData
            {
                Id = "proj-single",
                Type = ProjectType.OfficeRenovation,
                DisplayName = "Single Phase Project",
                ContractValue = 50000,
                DeadlineIso = _date.AddDays(60).ToString("o"),
                Status = ProjectStatus.Available,
                // final_inspection's prerequisites (interior_finish, roofing)
                // aren't part of this project's phase set, so they're
                // auto-satisfied and it becomes Ready immediately.
                Phases = { new PhaseRuntimeData { PhaseId = "final_inspection", DisplayName = "Final Inspection" } }
            };

            _projects.StartProject(project, _date);

            var worker = _crews.HireWorker(WorkerType.SiteSupervisor, "Jamie");
            var crew = _crews.CreateCrew("Supervisor Crew", new[] { worker.Id });
            _projects.AssignCrewToPhase(project.Id, "final_inspection", crew.Id);

            float reputationBefore = _company.Data.Reputation;

            const int generousDayCount = 20; // final_inspection is indoor (no weather penalty) and needs no equipment
            for (int i = 0; i < generousDayCount; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(_date.AddDays(i + 1)));
            }

            Assert.AreEqual(ProjectStatus.Completed, project.Status);
            Assert.Greater(QualityScoring.ScoreToLevel(project.QualityScore), _content.GetProjectTemplate(project.Type).RequiredQuality);
            // This fixture exceeds the required quality and earns the configured prototype 2% bonus.
            Assert.AreEqual(project.ContractValue * 1.02, project.CashReceivedToDate);
            Assert.Greater(_company.Data.Reputation, reputationBefore);
            Assert.AreEqual(1, _projects.CompletedProjects.Count);
        }

        [Test]
        public void GetSubcontractorQuotes_ForSpecialtyPhase_ReturnsQuotes()
        {
            var quotes = _projects.GetSubcontractorQuotesForPhase("irrelevant-project-id", "electrical_rough");
            Assert.Greater(quotes.Count, 0);
        }

        [Test]
        public void GetSubcontractorQuotes_ForNonSpecialtyPhase_ReturnsEmpty()
        {
            var quotes = _projects.GetSubcontractorQuotesForPhase("irrelevant-project-id", "site_prep");
            Assert.AreEqual(0, quotes.Count);
        }

        [Test]
        public void AwardSubcontract_ChargesCashAndMarksPhaseSubcontracted()
        {
            // electrical_rough's only prerequisite ("framing") isn't part of
            // this project's phase set, so it's Ready immediately.
            var project = new ProjectRuntimeData
            {
                Id = "proj-elec",
                Type = ProjectType.OfficeRenovation,
                DisplayName = "Electrical Only",
                ContractValue = 40000,
                DeadlineIso = _date.AddDays(60).ToString("o"),
                Status = ProjectStatus.Available,
                Phases = { new PhaseRuntimeData { PhaseId = "electrical_rough", DisplayName = "Electrical (Rough-in)" } }
            };
            _projects.StartProject(project, _date);

            var quotes = _projects.GetSubcontractorQuotesForPhase(project.Id, "electrical_rough");
            var chosen = quotes.First();

            double cashBefore = _company.Data.Cash;
            bool awarded = _projects.AwardSubcontractToPhase(project.Id, "electrical_rough", chosen.SubcontractorId, _date);

            var phase = project.Phases.First(p => p.PhaseId == "electrical_rough");

            Assert.IsTrue(awarded);
            Assert.Less(_company.Data.Cash, cashBefore);
            Assert.IsTrue(phase.IsSubcontracted);
            Assert.AreEqual(PhaseStatus.InProgress, phase.Status);
        }

        [Test]
        public void SubcontractedPhase_CompletesViaScheduledEventNotDailyTick()
        {
            var project = new ProjectRuntimeData
            {
                Id = "proj-elec-2",
                Type = ProjectType.OfficeRenovation,
                DisplayName = "Electrical Only",
                ContractValue = 40000,
                DeadlineIso = _date.AddDays(60).ToString("o"),
                Status = ProjectStatus.Available,
                Phases = { new PhaseRuntimeData { PhaseId = "electrical_rough", DisplayName = "Electrical (Rough-in)" } }
            };
            _projects.StartProject(project, _date);

            var quotes = _projects.GetSubcontractorQuotesForPhase(project.Id, "electrical_rough");
            var chosen = quotes.First();
            _projects.AwardSubcontractToPhase(project.Id, "electrical_rough", chosen.SubcontractorId, _date);

            // No crew is ever assigned, so the daily-tick path can't be
            // what completes this - only the subcontractor's own scheduled
            // completion should. Advance generously past even a "delayed"
            // outcome.
            for (int i = 0; i < 40; i++)
            {
                _timeManager.AdvanceOneDayImmediate();
            }

            Assert.AreEqual(ProjectStatus.Completed, project.Status);
        }

        [Test]
        public void AssignCrewToPhase_OnSubcontractedPhase_Fails()
        {
            var project = new ProjectRuntimeData
            {
                Id = "proj-elec-3",
                Type = ProjectType.OfficeRenovation,
                DisplayName = "Electrical Only",
                ContractValue = 40000,
                DeadlineIso = _date.AddDays(60).ToString("o"),
                Status = ProjectStatus.Available,
                Phases = { new PhaseRuntimeData { PhaseId = "electrical_rough", DisplayName = "Electrical (Rough-in)" } }
            };
            _projects.StartProject(project, _date);

            var quotes = _projects.GetSubcontractorQuotesForPhase(project.Id, "electrical_rough");
            _projects.AwardSubcontractToPhase(project.Id, "electrical_rough", quotes.First().SubcontractorId, _date);

            var worker = _crews.HireWorker(WorkerType.Electrician, "Pat");
            var crew = _crews.CreateCrew("Crew", new[] { worker.Id });

            bool assigned = _projects.AssignCrewToPhase(project.Id, "electrical_rough", crew.Id);

            Assert.IsFalse(assigned);
        }

        [Test]
        public void WellResourcedInspectedPhase_PassesInspectionAndRecordsQuality()
        {
            // "foundation" requires an inspection (spec section 22). Fully
            // staffing, equipping, and supplying it should comfortably
            // clear the pass threshold.
            var project = new ProjectRuntimeData
            {
                Id = "proj-foundation",
                Type = ProjectType.HouseRenovation,
                DisplayName = "Well-Resourced Foundation",
                ContractValue = 100000,
                DeadlineIso = _date.AddDays(90).ToString("o"),
                Status = ProjectStatus.Available,
                // "site_prep" isn't part of this project's phase set, so
                // foundation's prerequisite on it is auto-satisfied and it
                // starts Ready immediately.
                Phases = { new PhaseRuntimeData { PhaseId = "foundation", DisplayName = "Foundation" } }
            };
            _projects.StartProject(project, _date);

            var worker = _crews.HireWorker(WorkerType.Mason, "Jordan");
            var crew = _crews.CreateCrew("Foundation Crew", new[] { worker.Id });
            _projects.AssignCrewToPhase(project.Id, "foundation", crew.Id);

            var mixer = _equipment.PurchaseEquipment(EquipmentType.ConcreteMixer);
            _projects.AssignEquipmentToPhase(project.Id, "foundation", mixer.Id);

            _projects.OrderMaterialsForPhase(project.Id, "foundation", _date);

            bool inspectionPassed = false;
            EventBus.Subscribe<InspectionPassedEvent>(_ => inspectionPassed = true);

            for (int i = 0; i < 60; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(_date.AddDays(i + 1)));
            }

            var phase = project.Phases.First(p => p.PhaseId == "foundation");

            Assert.AreEqual(PhaseStatus.Completed, phase.Status);
            Assert.IsTrue(inspectionPassed);
            Assert.Greater(phase.QualityScore, 0.4f);
        }

        [Test]
        public void SubcontractedPhase_SkipsInspectionEvenWithTerribleQuality()
        {
            // A deliberately terrible custom subcontractor (BaseQuality
            // well below the in-house inspection threshold) - proves
            // subcontracted work is exempt from the inspection gate
            // regardless of how low its quality is, rather than relying on
            // any of the seeded subcontractors happening to be bad enough.
            var terribleSub = ScriptableObject.CreateInstance<SubcontractorDefinition>();
            terribleSub.Id = "sub_test_terrible";
            terribleSub.CompanyName = "Terrible Electric Co.";
            terribleSub.Specialty = SubcontractorSpecialty.Electrical;
            terribleSub.BaseCostMultiplier = 0.5f;
            terribleSub.BaseReliability = 1f; // always on time, to keep the test's timing simple
            terribleSub.BaseQuality = 0.1f;   // far below InspectionPassQuality
            terribleSub.SpeedFactor = 1f;
            _content.RegisterSubcontractor(terribleSub);

            var project = new ProjectRuntimeData
            {
                Id = "proj-terrible-sub",
                Type = ProjectType.OfficeRenovation,
                DisplayName = "Terrible Subcontractor Test",
                ContractValue = 40000,
                DeadlineIso = _date.AddDays(60).ToString("o"),
                Status = ProjectStatus.Available,
                Phases = { new PhaseRuntimeData { PhaseId = "electrical_rough", DisplayName = "Electrical (Rough-in)" } }
            };
            _projects.StartProject(project, _date);

            bool inspectionFailed = false;
            EventBus.Subscribe<InspectionFailedEvent>(_ => inspectionFailed = true);

            bool awarded = _projects.AwardSubcontractToPhase(project.Id, "electrical_rough", terribleSub.Id, _date);
            Assert.IsTrue(awarded);

            for (int i = 0; i < 20; i++)
            {
                _timeManager.AdvanceOneDayImmediate();
            }

            var phase = project.Phases.First(p => p.PhaseId == "electrical_rough");

            Assert.AreEqual(PhaseStatus.Completed, phase.Status);
            Assert.AreEqual(0.1f, phase.QualityScore);
            Assert.IsFalse(inspectionFailed);
        }

        [Test]
        public void CompleteProject_OnTime_ImprovesClientRelationship()
        {
            var clients = new ClientSystem(new SeededRandom(42));
            var client = clients.Clients.First();
            client.RelationshipScore = 40f;

            var projectsWithClient = new ConstructionProjectSystem(
                _content, _company, _materials, _crews, _equipment, _subcontractors, _weather, _timeManager, clients);

            var project = new ProjectRuntimeData
            {
                Id = "proj-client-rel",
                Type = ProjectType.OfficeRenovation,
                DisplayName = "Relationship Test Project",
                ClientId = client.Id,
                ContractValue = 40000,
                DeadlineIso = _date.AddDays(90).ToString("o"),
                Status = ProjectStatus.Available,
                Phases = { new PhaseRuntimeData { PhaseId = "final_inspection", DisplayName = "Final Inspection" } }
            };
            projectsWithClient.StartProject(project, _date);

            var worker = _crews.HireWorker(WorkerType.SiteSupervisor, "Casey");
            var crew = _crews.CreateCrew("Supervisor Crew", new[] { worker.Id });
            projectsWithClient.AssignCrewToPhase(project.Id, "final_inspection", crew.Id);

            for (int i = 0; i < 20; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(_date.AddDays(i + 1)));
            }

            Assert.AreEqual(ProjectStatus.Completed, project.Status);
            Assert.Greater(client.RelationshipScore, 40f);

            projectsWithClient.Dispose();
        }
    }
}

