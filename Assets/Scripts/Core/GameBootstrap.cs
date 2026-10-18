using System;
using UnityEngine;
using ConstructionEmpire.Bidding;
using ConstructionEmpire.Clients;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Config;
using ConstructionEmpire.Content;
using ConstructionEmpire.Equipment;
using ConstructionEmpire.Finance;
using ConstructionEmpire.Materials;
using ConstructionEmpire.Persistence;
using ConstructionEmpire.Projects;
using ConstructionEmpire.Safety;
using ConstructionEmpire.Subcontractors;
using ConstructionEmpire.UI;
using ConstructionEmpire.Utilities;
using ConstructionEmpire.Weather;
using ConstructionEmpire.Workforce;

namespace ConstructionEmpire.Core
{
    /// <summary>
    /// Composition root. Place this on a single GameObject in the
    /// Bootstrap scene, assign a GameConfig asset, and it wires up every
    /// core and gameplay system and either loads an existing save or
    /// founds a new company with a fresh set of project opportunities.
    /// Nothing else in the project should construct these systems
    /// directly - go through GameContext once this has run.
    /// </summary>
    [RequireComponent(typeof(GameTimeManager))]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("If set, tries to load this save slot on startup instead of founding a new company.")]
        [SerializeField] private string autoLoadSlot = "";

        [Tooltip("How many project opportunities to have on the board when a new company is founded.")]
        [SerializeField] private int startingOpportunityCount = 4;

        private void Awake()
        {
            if (gameConfig == null)
            {
                Debug.LogError("[GameBootstrap] No GameConfig assigned. Create one via Assets > Create > ConstructionEmpire > Game Config and assign it in the Inspector.");
                enabled = false;
                return;
            }

            var timeManager = GetComponent<GameTimeManager>();
            var startDate = new DateTime(gameConfig.StartingYear, gameConfig.StartingMonth, gameConfig.StartingDay);
            timeManager.Initialize(gameConfig.RealSecondsPerGameHour, startDate);

            var random = new SeededRandom(gameConfig.RandomSeed);
            var content = DefaultContentFactory.BuildDefaultLibrary();

            var simulationManager = new SimulationManager();
            var companySystem = new CompanySystem();
            var materialSystem = new MaterialSystem(content, companySystem, simulationManager);
            var crewSystem = new CrewSystem(content, companySystem);
            var equipmentSystem = new EquipmentSystem(content, companySystem, simulationManager, random);
            var weatherSystem = new WeatherSystem(random);
            var clientSystem = new ClientSystem(random);
            var estimationSystem = new EstimationSystem(content);
            var competitorSimulator = new CompetitorSimulator(random);
            var biddingSystem = new BiddingSystem(competitorSimulator, companySystem, random, clientSystem);
            var projectGenerationSystem = new ProjectGenerationSystem(content, random, clientSystem);
            var subcontractorSystem = new SubcontractorSystem(content, companySystem, simulationManager, random);
            var constructionProjectSystem = new ConstructionProjectSystem(
                content, companySystem, materialSystem, crewSystem, equipmentSystem,
                subcontractorSystem, weatherSystem, timeManager, clientSystem);
            var safetyIncidentSystem = new SafetyIncidentSystem(constructionProjectSystem, crewSystem, companySystem, random);
            var financeSystem = new FinanceSystem(timeManager);

            GameContext.Initialize(
                gameConfig, timeManager, simulationManager, companySystem, random,
                content, materialSystem, crewSystem, equipmentSystem,
                estimationSystem, biddingSystem, projectGenerationSystem, constructionProjectSystem, financeSystem,
                subcontractorSystem, weatherSystem, safetyIncidentSystem, clientSystem);

            bool loaded = !string.IsNullOrEmpty(autoLoadSlot) &&
                          SaveSystem.LoadGame(autoLoadSlot, companySystem, materialSystem, constructionProjectSystem, crewSystem, equipmentSystem, financeSystem, clientSystem);

            if (!loaded)
            {
                companySystem.FoundCompany(
                    gameConfig.DefaultCompanyName,
                    gameConfig.StartingCash,
                    gameConfig.StartingReputation,
                    timeManager.CurrentDate);

                var opportunities = projectGenerationSystem.GenerateOpportunities(startingOpportunityCount, timeManager.CurrentDate);
                constructionProjectSystem.SetOpportunities(opportunities);
            }

            Debug.Log("[GameBootstrap] Ready. Company: " + companySystem.Data.CompanyName +
                      ", Cash: " + companySystem.Data.Cash.ToString("C0") +
                      ", Date: " + timeManager.CurrentDate.ToString("d") +
                      ", Opportunities: " + constructionProjectSystem.AvailableOpportunities.Count);

            if (GetComponent<GameHUD>() == null)
            {
                gameObject.AddComponent<GameHUD>();
            }
        }

        private void OnDestroy()
        {
            GameContext.Reset();
        }
    }
}
