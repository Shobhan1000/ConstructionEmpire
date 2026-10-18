using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ConstructionEmpire.Bidding;
using ConstructionEmpire.Clients;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Equipment;
using ConstructionEmpire.Persistence;
using ConstructionEmpire.Projects;
using ConstructionEmpire.Safety;

namespace ConstructionEmpire.UI
{
    /// <summary>
    /// A functional, deliberately plain IMGUI HUD so the game is actually
    /// playable by pressing Play - no Canvas, no prefabs, no scene setup
    /// beyond GameBootstrap itself, since it auto-attaches this component
    /// (see GameBootstrap.Awake). This is a stand-in for the real
    /// Milestone 4 management UI, which needs the Unity Editor directly
    /// (RectTransform layout, prefabs) to build properly - everything
    /// here calls straight into the same GameContext systems that UI
    /// would eventually use, so replacing this later doesn't touch any
    /// simulation code.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private enum Tab { Opportunities, Projects, Workforce, Equipment, Log }

        private Tab _currentTab = Tab.Opportunities;

        private Vector2 _opportunitiesScroll;
        private Vector2 _projectsScroll;
        private Vector2 _workforceScroll;
        private Vector2 _equipmentScroll;
        private Vector2 _logScroll;

        private readonly List<string> _log = new List<string>();
        private const int MaxLogLines = 200;

        private int _nextWorkerNameIndex;
        private int _nextCrewNumber = 1;

        private static readonly string[] WorkerNamePool =
        {
            "Alex", "Sam", "Jordan", "Casey", "Taylor", "Morgan", "Riley", "Jamie", "Drew", "Cameron", "Avery", "Quinn"
        };

        private void Start()
        {
            EventBus.Subscribe<BidWonEvent>(e => Log("Bid won."));
            EventBus.Subscribe<BidLostEvent>(e => Log("Bid lost to " + e.WinningCompetitor + "."));
            EventBus.Subscribe<ProjectStartedEvent>(e => Log("Project started."));
            EventBus.Subscribe<PhaseCompletedEvent>(e => Log("Phase completed: " + e.PhaseId));
            EventBus.Subscribe<ProjectCompletedEvent>(e => Log("Project completed - " + (e.OnTime ? "on time" : "late") + ", profit " + e.FinalProfit.ToString("C0")));
            EventBus.Subscribe<InspectionFailedEvent>(e => Log("Inspection FAILED on " + e.PhaseId + " - rework needed."));
            EventBus.Subscribe<InspectionPassedEvent>(e => Log("Inspection passed on " + e.PhaseId + "."));
            EventBus.Subscribe<SubcontractAwardedEvent>(e => Log("Subcontract awarded for " + e.PhaseId + "."));
            EventBus.Subscribe<SafetyIncidentEvent>(e => Log("Safety incident on " + e.PhaseId + " - cost " + e.InvestigationCost.ToString("C0") + "."));
            EventBus.Subscribe<EquipmentBrokenDownEvent>(e => Log("Equipment broke down!"));

            Log("Welcome. Bid on an opportunity to get started.");
        }

        private void OnGUI()
        {
            if (!GameContext.IsInitialized || GameContext.Company.Data == null)
            {
                GUILayout.Label("Initializing...");
                return;
            }

            DrawHeaderBar();
            DrawTabBar();

            GUILayout.BeginArea(new Rect(10, 100, Screen.width - 20, Screen.height - 110));
            switch (_currentTab)
            {
                case Tab.Opportunities: DrawOpportunitiesTab(); break;
                case Tab.Projects: DrawProjectsTab(); break;
                case Tab.Workforce: DrawWorkforceTab(); break;
                case Tab.Equipment: DrawEquipmentTab(); break;
                case Tab.Log: DrawLogTab(); break;
            }
            GUILayout.EndArea();
        }

        private void DrawHeaderBar()
        {
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, 50));
            GUILayout.BeginHorizontal("box");

            var company = GameContext.Company.Data;
            string status = company.CompanyName + "   |   Cash: " + company.Cash.ToString("C0") +
                             "   |   Reputation: " + company.Reputation.ToString("F0") +
                             "   |   " + GameContext.Time.CurrentDate.ToString("d") +
                             "   |   Weather: " + GameContext.Weather.CurrentWeather;
            GUILayout.Label(status);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(GameContext.Time.IsPaused ? "Resume" : "Pause", GUILayout.Width(65)))
            {
                if (GameContext.Time.IsPaused) GameContext.Time.Resume(); else GameContext.Time.Pause();
            }
            if (GUILayout.Button("1x", GUILayout.Width(30))) GameContext.Time.SetTimeScale(TimeScale.Normal);
            if (GUILayout.Button("2x", GUILayout.Width(30))) GameContext.Time.SetTimeScale(TimeScale.Fast2x);
            if (GUILayout.Button("5x", GUILayout.Width(30))) GameContext.Time.SetTimeScale(TimeScale.Fast5x);
            if (GUILayout.Button("10x", GUILayout.Width(35))) GameContext.Time.SetTimeScale(TimeScale.Fast10x);

            GUILayout.Space(10);
            if (GUILayout.Button("Save", GUILayout.Width(55))) SaveGame();
            if (GUILayout.Button("Load", GUILayout.Width(55))) LoadGame();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawTabBar()
        {
            GUILayout.BeginArea(new Rect(10, 65, Screen.width - 20, 30));
            string[] tabNames = { "Opportunities", "Active Projects", "Workforce", "Equipment", "Log" };
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, tabNames);
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------
        // Opportunities
        // ---------------------------------------------------------------

        private void DrawOpportunitiesTab()
        {
            GUILayout.Label("Available Opportunities");

            if (GUILayout.Button("Find More Opportunities (+3)", GUILayout.Width(220)))
            {
                GenerateMoreOpportunities();
            }

            GUILayout.Space(6);
            _opportunitiesScroll = GUILayout.BeginScrollView(_opportunitiesScroll);

            foreach (var opportunity in GameContext.Projects.AvailableOpportunities.ToList())
            {
                GUILayout.BeginVertical("box");

                var client = GameContext.Clients.GetClient(opportunity.ClientId);
                string clientLabel = client != null
                    ? client.Name + " (" + client.GetRelationshipLevel() + ")"
                    : opportunity.ClientName;

                GUILayout.Label(opportunity.DisplayName + "  -  Client: " + clientLabel);
                GUILayout.Label("Estimated value: " + opportunity.ContractValue.ToString("C0") + "   Deadline: " + FormatDate(opportunity.DeadlineIso));

                var template = GameContext.Content.GetProjectTemplate(opportunity.Type);
                var phaseIds = opportunity.Phases.Select(p => p.PhaseId).ToList();
                var estimate = GameContext.Estimation.Estimate(template, phaseIds, 0.10f, 0.15f);

                GUILayout.Label("Recommended bid: " + estimate.RecommendedBidPrice.ToString("C0") +
                                "   (cost " + estimate.Subtotal.ToString("C0") + " + contingency + 15% profit)");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Bid at recommended price", GUILayout.Width(220)))
                {
                    SubmitBid(opportunity, estimate.RecommendedBidPrice, estimate.Subtotal);
                }
                if (GUILayout.Button("Bid aggressively (-15%, riskier margin)", GUILayout.Width(280)))
                {
                    SubmitBid(opportunity, estimate.RecommendedBidPrice * 0.85, estimate.Subtotal);
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.Space(4);
            }

            if (GameContext.Projects.AvailableOpportunities.Count == 0)
            {
                GUILayout.Label("No opportunities on the board - click \"Find More Opportunities\" above.");
            }

            GUILayout.EndScrollView();
        }

        private void SubmitBid(ProjectRuntimeData opportunity, double bidPrice, double fairMarketCost)
        {
            bool won = GameContext.Bidding.SubmitBid(opportunity, bidPrice, fairMarketCost);
            if (won)
            {
                GameContext.Projects.StartProject(opportunity, GameContext.Time.CurrentDate);
            }
        }

        private void GenerateMoreOpportunities()
        {
            var newOnes = GameContext.ProjectGeneration.GenerateOpportunities(3, GameContext.Time.CurrentDate);
            var combined = GameContext.Projects.AvailableOpportunities.Concat(newOnes).ToList();
            GameContext.Projects.SetOpportunities(combined);
        }

        // ---------------------------------------------------------------
        // Active Projects
        // ---------------------------------------------------------------

        private void DrawProjectsTab()
        {
            GUILayout.Label("Active Projects");
            _projectsScroll = GUILayout.BeginScrollView(_projectsScroll);

            foreach (var project in GameContext.Projects.ActiveProjects.ToList())
            {
                GUILayout.BeginVertical("box");

                GUILayout.Label(project.DisplayName + "  -  Contract: " + project.ContractValue.ToString("C0") +
                                "   Deadline: " + FormatDate(project.DeadlineIso));
                GUILayout.Label("Received so far: " + project.CashReceivedToDate.ToString("C0") +
                                "   Quality so far: " + QualityScoring.ScoreToLevel(project.QualityScore));

                foreach (var phase in project.Phases)
                {
                    DrawPhaseRow(project, phase);
                }

                GUILayout.EndVertical();
                GUILayout.Space(6);
            }

            if (GameContext.Projects.ActiveProjects.Count == 0)
            {
                GUILayout.Label("No active projects. Bid on an opportunity to get started.");
            }

            GUILayout.EndScrollView();
        }

        private void DrawPhaseRow(ProjectRuntimeData project, PhaseRuntimeData phase)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(phase.DisplayName + " [" + phase.Status + "]", GUILayout.Width(230));
            GUILayout.Label((phase.Progress * 100f).ToString("F0") + "%", GUILayout.Width(45));

            bool canAssign = !phase.IsSubcontracted && (phase.Status == PhaseStatus.Ready || phase.Status == PhaseStatus.InProgress);
            if (canAssign)
            {
                if (GUILayout.Button("Assign Idle Crew", GUILayout.Width(130)))
                {
                    AssignIdleCrew(project, phase);
                }

                var phaseDef = GameContext.Content.GetPhase(phase.PhaseId);
                if (phaseDef != null && phaseDef.RequiredEquipment != null && phaseDef.RequiredEquipment.Length > 0)
                {
                    if (GUILayout.Button("Assign Equipment", GUILayout.Width(130)))
                    {
                        AssignAvailableEquipment(project, phase, phaseDef);
                    }
                }

                if (phaseDef != null && phaseDef.RequiredMaterials != null && phaseDef.RequiredMaterials.Length > 0)
                {
                    if (GUILayout.Button("Order Materials", GUILayout.Width(130)))
                    {
                        GameContext.Projects.OrderMaterialsForPhase(project.Id, phase.PhaseId, GameContext.Time.CurrentDate);
                    }
                }
            }

            GUILayout.EndHorizontal();

            if (phase.Status == PhaseStatus.Ready)
            {
                var phaseDef = GameContext.Content.GetPhase(phase.PhaseId);
                if (phaseDef != null && phaseDef.RelevantSpecialty != SubcontractorSpecialty.None)
                {
                    var quotes = GameContext.Projects.GetSubcontractorQuotesForPhase(project.Id, phase.PhaseId);
                    if (quotes.Count > 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("  Subcontract instead:", GUILayout.Width(140));
                        foreach (var quote in quotes)
                        {
                            string label = quote.CompanyName + " (" + quote.Price.ToString("C0") + ", ~" +
                                           quote.EstimatedDurationDays + "d, " + (quote.Reliability * 100f).ToString("F0") + "% reliable)";
                            if (GUILayout.Button(label, GUILayout.Width(280)))
                            {
                                GameContext.Projects.AwardSubcontractToPhase(project.Id, phase.PhaseId, quote.SubcontractorId, GameContext.Time.CurrentDate);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }

        private void AssignIdleCrew(ProjectRuntimeData project, PhaseRuntimeData phase)
        {
            var idleCrew = GameContext.Crews.Crews.FirstOrDefault(c => string.IsNullOrEmpty(c.CurrentProjectId));
            if (idleCrew == null)
            {
                Log("No idle crew available - hire workers and create a crew first (Workforce tab).");
                return;
            }

            GameContext.Projects.AssignCrewToPhase(project.Id, phase.PhaseId, idleCrew.Id);
        }

        private void AssignAvailableEquipment(ProjectRuntimeData project, PhaseRuntimeData phase, ConstructionPhaseDefinition phaseDef)
        {
            foreach (var requirement in phaseDef.RequiredEquipment)
            {
                var available = GameContext.Equipment.Fleet.FirstOrDefault(e => e.Type == requirement.Type && e.Status == EquipmentStatus.Available);
                if (available != null)
                {
                    GameContext.Projects.AssignEquipmentToPhase(project.Id, phase.PhaseId, available.Id);
                }
                else
                {
                    Log("No available " + requirement.Type + " - purchase one in the Equipment tab.");
                }
            }
        }

        // ---------------------------------------------------------------
        // Workforce
        // ---------------------------------------------------------------

        private void DrawWorkforceTab()
        {
            GUILayout.Label("Hire:");
            GUILayout.BeginHorizontal();
            int column = 0;
            foreach (WorkerType type in Enum.GetValues(typeof(WorkerType)))
            {
                if (GUILayout.Button(type.ToString(), GUILayout.Width(140)))
                {
                    HireWorker(type);
                }
                column++;
                if (column % 5 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("Create Crew From All Idle Workers", GUILayout.Width(280)))
            {
                CreateCrewFromIdleWorkers();
            }

            GUILayout.Space(8);
            _workforceScroll = GUILayout.BeginScrollView(_workforceScroll);

            GUILayout.Label("Workers (" + GameContext.Crews.Workers.Count + "):");
            foreach (var worker in GameContext.Crews.Workers)
            {
                string state = string.IsNullOrEmpty(worker.CurrentCrewId) ? "Idle" : "In a crew";
                GUILayout.Label("  " + worker.Name + " (" + worker.Type + ")   Fatigue: " + worker.Fatigue.ToString("F0") +
                                "   Morale: " + worker.Morale.ToString("F0") + "   [" + state + "]");
            }

            GUILayout.Space(8);
            GUILayout.Label("Crews (" + GameContext.Crews.Crews.Count + "):");
            foreach (var crew in GameContext.Crews.Crews)
            {
                string state = string.IsNullOrEmpty(crew.CurrentProjectId) ? "Idle" : "Working";
                GUILayout.Label("  " + crew.Name + "   " + crew.WorkerIds.Count + " worker(s)   [" + state + "]");
            }

            GUILayout.EndScrollView();
        }

        private void HireWorker(WorkerType type)
        {
            string first = WorkerNamePool[_nextWorkerNameIndex % WorkerNamePool.Length];
            int generation = (_nextWorkerNameIndex / WorkerNamePool.Length) + 1;
            string name = generation > 1 ? first + " " + generation : first;
            _nextWorkerNameIndex++;

            GameContext.Crews.HireWorker(type, name);
        }

        private void CreateCrewFromIdleWorkers()
        {
            var idleWorkerIds = GameContext.Crews.Workers
                .Where(w => string.IsNullOrEmpty(w.CurrentCrewId))
                .Select(w => w.Id)
                .ToList();

            if (idleWorkerIds.Count == 0)
            {
                Log("No idle workers to form a crew from - hire some first.");
                return;
            }

            GameContext.Crews.CreateCrew("Crew " + _nextCrewNumber, idleWorkerIds);
            _nextCrewNumber++;
        }

        // ---------------------------------------------------------------
        // Equipment
        // ---------------------------------------------------------------

        private void DrawEquipmentTab()
        {
            GUILayout.Label("Purchase:");
            GUILayout.BeginHorizontal();
            int column = 0;
            foreach (EquipmentType type in Enum.GetValues(typeof(EquipmentType)))
            {
                var def = GameContext.Content.GetEquipment(type);
                string label = type + " (" + (def != null ? def.PurchasePrice.ToString("C0") : "?") + ")";
                if (GUILayout.Button(label, GUILayout.Width(170)))
                {
                    GameContext.Equipment.PurchaseEquipment(type);
                }
                column++;
                if (column % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            _equipmentScroll = GUILayout.BeginScrollView(_equipmentScroll);

            GUILayout.Label("Fleet (" + GameContext.Equipment.Fleet.Count + "):");
            foreach (var item in GameContext.Equipment.Fleet)
            {
                string ownership = item.IsOwned ? "Owned" : "Rented";
                GUILayout.Label("  " + item.DisplayName + "   Condition: " + item.Condition.ToString("F0") +
                                "%   Status: " + item.Status + "   [" + ownership + "]");
            }

            GUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------

        private void DrawLogTab()
        {
            GUILayout.Label("Recent Events (most recent first):");
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            foreach (var line in _log)
            {
                GUILayout.Label(line);
            }
            GUILayout.EndScrollView();
        }

        private void Log(string message)
        {
            string dateLabel = GameContext.IsInitialized ? GameContext.Time.CurrentDate.ToString("d") : "-";
            _log.Insert(0, "[" + dateLabel + "] " + message);
            if (_log.Count > MaxLogLines)
            {
                _log.RemoveAt(_log.Count - 1);
            }
        }

        // ---------------------------------------------------------------
        // Save / Load
        // ---------------------------------------------------------------

        private const string SaveSlot = "default";

        private void SaveGame()
        {
            bool saved = SaveSystem.SaveGame(
                SaveSlot, GameContext.Company, GameContext.Materials, GameContext.Projects,
                GameContext.Crews, GameContext.Equipment, GameContext.Finance, GameContext.Clients);
            Log(saved ? "Game saved." : "Save failed - check the Console.");
        }

        private void LoadGame()
        {
            bool loaded = SaveSystem.LoadGame(
                SaveSlot, GameContext.Company, GameContext.Materials, GameContext.Projects,
                GameContext.Crews, GameContext.Equipment, GameContext.Finance, GameContext.Clients);
            Log(loaded ? "Game loaded." : "Load failed - no save found, or check the Console.");
        }

        private static string FormatDate(string isoDate)
        {
            return DateTime.TryParse(isoDate, out var date) ? date.ToString("d") : "?";
        }
    }
}
