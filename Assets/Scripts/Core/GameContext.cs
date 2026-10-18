using ConstructionEmpire.Bidding;
using ConstructionEmpire.Clients;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Config;
using ConstructionEmpire.Content;
using ConstructionEmpire.Equipment;
using ConstructionEmpire.Finance;
using ConstructionEmpire.Materials;
using ConstructionEmpire.Projects;
using ConstructionEmpire.Safety;
using ConstructionEmpire.Subcontractors;
using ConstructionEmpire.Utilities;
using ConstructionEmpire.Weather;
using ConstructionEmpire.Workforce;

namespace ConstructionEmpire.Core
{
    /// <summary>
    /// Lightweight service locator for the systems created by
    /// GameBootstrap. This holds references only - it must never contain
    /// gameplay logic itself (spec section 62: "do not create one giant
    /// GameManager"). Systems wire to each other via constructor
    /// injection; GameContext exists purely so UI and tests, which run
    /// after everything is built, have one place to find them.
    ///
    /// NOTE: Initialize's parameter list has grown long across a few
    /// milestones. It's still correct (every call site is checked against
    /// this signature), but it's a good candidate for a small "GameSystems"
    /// registry struct in a future cleanup pass rather than continuing to
    /// grow positionally forever.
    /// </summary>
    public static class GameContext
    {
        public static GameConfig Config { get; private set; }
        public static GameTimeManager Time { get; private set; }
        public static SimulationManager Simulation { get; private set; }
        public static CompanySystem Company { get; private set; }
        public static SeededRandom Random { get; private set; }

        public static ContentLibrary Content { get; private set; }
        public static MaterialSystem Materials { get; private set; }
        public static CrewSystem Crews { get; private set; }
        public static EquipmentSystem Equipment { get; private set; }
        public static EstimationSystem Estimation { get; private set; }
        public static BiddingSystem Bidding { get; private set; }
        public static ProjectGenerationSystem ProjectGeneration { get; private set; }
        public static ConstructionProjectSystem Projects { get; private set; }
        public static FinanceSystem Finance { get; private set; }
        public static SubcontractorSystem Subcontractors { get; private set; }
        public static WeatherSystem Weather { get; private set; }
        public static SafetyIncidentSystem Safety { get; private set; }
        public static ClientSystem Clients { get; private set; }

        public static bool IsInitialized { get; private set; }

        public static void Initialize(
            GameConfig config,
            GameTimeManager time,
            SimulationManager simulation,
            CompanySystem company,
            SeededRandom random,
            ContentLibrary content,
            MaterialSystem materials,
            CrewSystem crews,
            EquipmentSystem equipment,
            EstimationSystem estimation,
            BiddingSystem bidding,
            ProjectGenerationSystem projectGeneration,
            ConstructionProjectSystem projects,
            FinanceSystem finance,
            SubcontractorSystem subcontractors,
            WeatherSystem weather,
            SafetyIncidentSystem safety,
            ClientSystem clients)
        {
            Config = config;
            Time = time;
            Simulation = simulation;
            Company = company;
            Random = random;

            Content = content;
            Materials = materials;
            Crews = crews;
            Equipment = equipment;
            Estimation = estimation;
            Bidding = bidding;
            ProjectGeneration = projectGeneration;
            Projects = projects;
            Finance = finance;
            Subcontractors = subcontractors;
            Weather = weather;
            Safety = safety;
            Clients = clients;

            IsInitialized = true;
        }

        public static void Reset()
        {
            Simulation?.Dispose();
            Crews?.Dispose();
            Equipment?.Dispose();
            Projects?.Dispose();
            Finance?.Dispose();
            Weather?.Dispose();
            Safety?.Dispose();

            Config = null;
            Time = null;
            Simulation = null;
            Company = null;
            Random = null;

            Content = null;
            Materials = null;
            Crews = null;
            Equipment = null;
            Estimation = null;
            Bidding = null;
            ProjectGeneration = null;
            Projects = null;
            Finance = null;
            Subcontractors = null;
            Weather = null;
            Safety = null;
            Clients = null;

            IsInitialized = false;
            EventBus.Clear();
        }
    }
}
