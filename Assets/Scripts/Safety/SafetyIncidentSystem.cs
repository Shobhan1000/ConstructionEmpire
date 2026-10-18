using System.Linq;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Projects;
using ConstructionEmpire.Utilities;
using ConstructionEmpire.Workforce;

namespace ConstructionEmpire.Safety
{
    public readonly struct SafetyIncidentEvent : IGameEvent
    {
        public readonly string ProjectId;
        public readonly string PhaseId;
        public readonly double InvestigationCost;
        public SafetyIncidentEvent(string projectId, string phaseId, double investigationCost)
        {
            ProjectId = projectId;
            PhaseId = phaseId;
            InvestigationCost = investigationCost;
        }
    }

    /// <summary>
    /// A lightweight safety-incident roll tied directly to crew fatigue
    /// (spec section 21) - gives the fatigue mechanic real stakes beyond
    /// its existing productivity penalty. Deliberately not graphic: an
    /// incident is a management event (cost, delay, reputation), never
    /// depicted. Only affects in-house, crewed, in-progress phases -
    /// subcontracted work carries its own (the subcontractor's) risk,
    /// already priced into their reliability roll.
    /// </summary>
    public class SafetyIncidentSystem
    {
        private const float BaseDailyIncidentChance = 0.01f; // 1% baseline per in-progress crewed phase per day
        private const float MaxFatigueAddedChance = 0.06f;   // up to +6% more at maximum fatigue
        private const float ProgressSetbackOnIncident = 0.05f;

        private readonly ConstructionProjectSystem _projectSystem;
        private readonly CrewSystem _crewSystem;
        private readonly CompanySystem _companySystem;
        private readonly SeededRandom _random;

        public SafetyIncidentSystem(ConstructionProjectSystem projectSystem, CrewSystem crewSystem, CompanySystem companySystem, SeededRandom random)
        {
            _projectSystem = projectSystem;
            _crewSystem = crewSystem;
            _companySystem = companySystem;
            _random = random;

            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        private void OnDayAdvanced(DayAdvancedEvent evt)
        {
            foreach (var project in _projectSystem.ActiveProjects.ToList())
            {
                var eligiblePhases = project.Phases.Where(p =>
                    p.Status == PhaseStatus.InProgress && !p.IsSubcontracted && p.AssignedCrewIds.Count > 0);

                foreach (var phase in eligiblePhases)
                {
                    float averageFatigue = GetAverageFatigue(phase);
                    float chance = BaseDailyIncidentChance + (averageFatigue / 100f) * MaxFatigueAddedChance;

                    if (_random.NextBool(chance))
                    {
                        TriggerIncident(project, phase);
                    }
                }
            }
        }

        private float GetAverageFatigue(PhaseRuntimeData phase)
        {
            float total = 0f;
            int counted = 0;

            foreach (var crewId in phase.AssignedCrewIds)
            {
                float fatigue = _crewSystem.GetCrewAverageFatigue(crewId);
                if (fatigue >= 0f)
                {
                    total += fatigue;
                    counted++;
                }
            }

            return counted > 0 ? total / counted : 0f;
        }

        private void TriggerIncident(ProjectRuntimeData project, PhaseRuntimeData phase)
        {
            double investigationCost = _random.NextFloat(2000f, 6000f);
            _companySystem.TrySpendCash(investigationCost, "Safety incident investigation: " + project.DisplayName);
            _companySystem.AdjustReputation(-2f, "Safety incident: " + project.DisplayName);
            _projectSystem.ApplyProgressSetback(project.Id, phase.PhaseId, ProgressSetbackOnIncident);

            EventBus.Publish(new SafetyIncidentEvent(project.Id, phase.PhaseId, investigationCost));
        }
    }
}
