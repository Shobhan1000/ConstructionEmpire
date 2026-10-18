using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Clients;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Equipment;
using ConstructionEmpire.Materials;
using ConstructionEmpire.Subcontractors;
using ConstructionEmpire.Weather;
using ConstructionEmpire.Workforce;

namespace ConstructionEmpire.Projects
{
    public readonly struct ProjectStartedEvent : IGameEvent
    {
        public readonly string ProjectId;
        public ProjectStartedEvent(string projectId) { ProjectId = projectId; }
    }

    public readonly struct PhaseCompletedEvent : IGameEvent
    {
        public readonly string ProjectId;
        public readonly string PhaseId;
        public PhaseCompletedEvent(string projectId, string phaseId) { ProjectId = projectId; PhaseId = phaseId; }
    }

    public readonly struct ProjectCompletedEvent : IGameEvent
    {
        public readonly string ProjectId;
        public readonly bool OnTime;
        public readonly double FinalProfit;
        public ProjectCompletedEvent(string projectId, bool onTime, double finalProfit)
        {
            ProjectId = projectId;
            OnTime = onTime;
            FinalProfit = finalProfit;
        }
    }

    public readonly struct SubcontractAwardedEvent : IGameEvent
    {
        public readonly string ProjectId;
        public readonly string PhaseId;
        public readonly string SubcontractorId;
        public SubcontractAwardedEvent(string projectId, string phaseId, string subcontractorId)
        {
            ProjectId = projectId;
            PhaseId = phaseId;
            SubcontractorId = subcontractorId;
        }
    }

    public readonly struct InspectionFailedEvent : IGameEvent
    {
        public readonly string ProjectId;
        public readonly string PhaseId;
        public readonly float QualityScore;
        public InspectionFailedEvent(string projectId, string phaseId, float qualityScore)
        {
            ProjectId = projectId;
            PhaseId = phaseId;
            QualityScore = qualityScore;
        }
    }

    public readonly struct InspectionPassedEvent : IGameEvent
    {
        public readonly string ProjectId;
        public readonly string PhaseId;
        public readonly float QualityScore;
        public InspectionPassedEvent(string projectId, string phaseId, float qualityScore)
        {
            ProjectId = projectId;
            PhaseId = phaseId;
            QualityScore = qualityScore;
        }
    }

    /// <summary>
    /// Owns active construction projects: phase dependency gating, daily
    /// progress, quality, inspections, payments, and completion (spec
    /// sections 6-8, 20, 22, and 37). Deliberately requires explicit
    /// crew/equipment/material assignment calls rather than auto-assigning
    /// anything - matches the spec's decision-driven design (section 65).
    /// A phase with nothing assigned simply sits idle. A phase can
    /// alternatively be handed to a subcontractor instead of staffed
    /// in-house - see AwardSubcontractToPhase.
    /// </summary>
    public class ConstructionProjectSystem
    {
        private const float InspectionPassQuality = 0.4f; // roughly the Poor/BelowAverage boundary
        private const float InspectionReworkSetback = 0.3f;

        private readonly ContentLibrary _content;
        private readonly CompanySystem _companySystem;
        private readonly MaterialSystem _materialSystem;
        private readonly CrewSystem _crewSystem;
        private readonly EquipmentSystem _equipmentSystem;
        private readonly SubcontractorSystem _subcontractorSystem;
        private readonly WeatherSystem _weatherSystem;
        private readonly GameTimeManager _timeManager;
        private readonly ClientSystem _clientSystem;

        public List<ProjectRuntimeData> AvailableOpportunities { get; private set; } = new List<ProjectRuntimeData>();
        public List<ProjectRuntimeData> ActiveProjects { get; private set; } = new List<ProjectRuntimeData>();
        public List<ProjectRuntimeData> CompletedProjects { get; private set; } = new List<ProjectRuntimeData>();

        public ConstructionProjectSystem(
            ContentLibrary content,
            CompanySystem companySystem,
            MaterialSystem materialSystem,
            CrewSystem crewSystem,
            EquipmentSystem equipmentSystem,
            SubcontractorSystem subcontractorSystem,
            WeatherSystem weatherSystem,
            GameTimeManager timeManager,
            ClientSystem clientSystem = null)
        {
            _content = content;
            _companySystem = companySystem;
            _materialSystem = materialSystem;
            _crewSystem = crewSystem;
            _equipmentSystem = equipmentSystem;
            _subcontractorSystem = subcontractorSystem;
            _weatherSystem = weatherSystem;
            _timeManager = timeManager;
            _clientSystem = clientSystem;

            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        public void SetOpportunities(List<ProjectRuntimeData> opportunities)
        {
            AvailableOpportunities = opportunities ?? new List<ProjectRuntimeData>();
        }

        /// <summary>Call after BiddingSystem.SubmitBid returns true for this project.</summary>
        public void StartProject(ProjectRuntimeData project, DateTime currentDate)
        {
            if (project == null) return;

            project.Status = ProjectStatus.InProgress;
            project.StartDateIso = currentDate.ToString("o");

            UnlockEligiblePhases(project);

            double upfront = project.ContractValue * 0.10;
            _companySystem.AddCash(upfront, "Upfront payment: " + project.DisplayName);
            project.CashReceivedToDate += upfront;
            project.UpfrontPaid = true;

            AvailableOpportunities.RemoveAll(p => p.Id == project.Id);
            if (!ActiveProjects.Any(p => p.Id == project.Id))
            {
                ActiveProjects.Add(project);
            }

            EventBus.Publish(new ProjectStartedEvent(project.Id));
        }

        public bool AssignCrewToPhase(string projectId, string phaseId, string crewId)
        {
            var phase = FindPhase(projectId, phaseId);
            if (phase == null || phase.IsSubcontracted) return false;
            if (phase.Status != PhaseStatus.Ready && phase.Status != PhaseStatus.InProgress) return false;

            if (!phase.AssignedCrewIds.Contains(crewId))
            {
                phase.AssignedCrewIds.Add(crewId);
            }

            _crewSystem.AssignCrewToProject(crewId, projectId, phaseId);

            if (phase.Status == PhaseStatus.Ready)
            {
                phase.Status = PhaseStatus.InProgress;
            }

            return true;
        }

        public bool AssignEquipmentToPhase(string projectId, string phaseId, string equipmentId)
        {
            var phase = FindPhase(projectId, phaseId);
            if (phase == null || phase.IsSubcontracted) return false;

            if (!phase.AssignedEquipmentIds.Contains(equipmentId))
            {
                phase.AssignedEquipmentIds.Add(equipmentId);
            }

            _equipmentSystem.AssignEquipmentToProject(equipmentId, projectId);
            return true;
        }

        /// <summary>Orders every material this phase needs that isn't already on hand or on order in sufficient quantity.</summary>
        public void OrderMaterialsForPhase(string projectId, string phaseId, DateTime currentDate)
        {
            var phase = FindPhase(projectId, phaseId);
            if (phase == null || phase.IsSubcontracted) return;

            var phaseDef = _content.GetPhase(phaseId);
            if (phaseDef == null) return;

            foreach (var requirement in phaseDef.RequiredMaterials)
            {
                double have = _materialSystem.GetAvailable(requirement.Type);
                double shortfall = requirement.Quantity - have;
                if (shortfall > 0)
                {
                    _materialSystem.OrderMaterial(requirement.Type, shortfall, "Phase: " + phaseDef.DisplayName, currentDate);
                }
            }
        }

        /// <summary>Quotes for handing this phase to a subcontractor instead of staffing it in-house. Empty if the phase has no RelevantSpecialty.</summary>
        public List<SubcontractorQuote> GetSubcontractorQuotesForPhase(string projectId, string phaseId)
        {
            var phaseDef = _content.GetPhase(phaseId);
            if (phaseDef == null) return new List<SubcontractorQuote>();

            return _subcontractorSystem.GetQuotes(phaseDef.RelevantSpecialty, phaseDef);
        }

        /// <summary>
        /// Awards the phase to a subcontractor instead of an in-house
        /// crew. The phase resolves on its own schedule (see
        /// SubcontractorSystem) rather than the daily crew-driven tick,
        /// and skips the inspection gate - subcontracted work is assumed
        /// to pass its own quality control (their reliability roll already
        /// prices in that risk).
        /// </summary>
        public bool AwardSubcontractToPhase(string projectId, string phaseId, string subcontractorId, DateTime currentDate)
        {
            var project = ActiveProjects.FirstOrDefault(p => p.Id == projectId);
            var phase = project != null ? project.Phases.FirstOrDefault(ph => ph.PhaseId == phaseId) : null;
            var phaseDef = phase != null ? _content.GetPhase(phaseId) : null;
            if (project == null || phase == null || phaseDef == null) return false;
            if (phase.Status != PhaseStatus.Ready) return false;

            bool awarded = _subcontractorSystem.AwardSubcontract(
                subcontractorId, phaseDef, currentDate, project.DisplayName,
                onTime => CompleteSubcontractedPhase(project.Id, phase.PhaseId));

            if (!awarded) return false;

            phase.Status = PhaseStatus.InProgress;
            phase.IsSubcontracted = true;
            phase.AssignedSubcontractorId = subcontractorId;

            EventBus.Publish(new SubcontractAwardedEvent(project.Id, phase.PhaseId, subcontractorId));
            return true;
        }

        /// <summary>Reduces a phase's progress (e.g. from a safety incident's rework/downtime), clamped at 0.</summary>
        public void ApplyProgressSetback(string projectId, string phaseId, float amount)
        {
            var phase = FindPhase(projectId, phaseId);
            if (phase == null) return;

            phase.Progress = Math.Max(0f, phase.Progress - amount);
        }

        private PhaseRuntimeData FindPhase(string projectId, string phaseId)
        {
            var project = ActiveProjects.FirstOrDefault(p => p.Id == projectId);
            return project != null ? project.Phases.FirstOrDefault(ph => ph.PhaseId == phaseId) : null;
        }

        private void UnlockEligiblePhases(ProjectRuntimeData project)
        {
            var completedIds = new HashSet<string>(project.Phases.Where(p => p.Status == PhaseStatus.Completed).Select(p => p.PhaseId));

            foreach (var phase in project.Phases)
            {
                if (phase.Status != PhaseStatus.Locked) continue;

                var phaseDef = _content.GetPhase(phase.PhaseId);
                var prerequisites = phaseDef != null ? phaseDef.PrerequisitePhaseIds : new string[0];

                bool allPrerequisitesMet = prerequisites.All(reqId =>
                    !project.Phases.Any(p => p.PhaseId == reqId) || completedIds.Contains(reqId));

                if (allPrerequisitesMet)
                {
                    phase.Status = PhaseStatus.Ready;
                }
            }
        }

        private void OnDayAdvanced(DayAdvancedEvent evt)
        {
            for (int i = ActiveProjects.Count - 1; i >= 0; i--)
            {
                AdvanceProject(ActiveProjects[i], evt.Date);
            }
        }

        private void AdvanceProject(ProjectRuntimeData project, DateTime currentDate)
        {
            bool anyPhaseCompletedToday = false;

            foreach (var phase in project.Phases)
            {
                if (phase.Status != PhaseStatus.InProgress) continue;
                if (phase.IsSubcontracted) continue; // resolves via its own scheduled completion, not the daily tick

                var phaseDef = _content.GetPhase(phase.PhaseId);
                if (phaseDef == null) continue;

                float crewFactor = ComputeCrewFactor(phase);
                if (crewFactor <= 0f) continue; // no crew assigned - phase sits idle, no progress, no cost

                float equipmentFactor = ComputeEquipmentFactor(phase, phaseDef);
                float materialFactor = ComputeMaterialFactor(phaseDef);
                float weatherFactor = _weatherSystem.GetProgressModifier(phaseDef.IsOutdoorWork);

                double baseDailyFraction = phaseDef.BaseDurationDays > 0 ? 1.0 / phaseDef.BaseDurationDays : 1.0;
                double dailyProgress = baseDailyFraction * crewFactor * equipmentFactor * materialFactor * weatherFactor;

                phase.Progress = (float)Math.Min(1.0, phase.Progress + dailyProgress);

                double dailyLaborCost = phaseDef.BaseLaborCost / Math.Max(1, phaseDef.BaseDurationDays);
                phase.CostIncurred += dailyLaborCost;
                project.CostIncurredToDate += dailyLaborCost;

                ConsumeMaterialsForProgress(phaseDef, dailyProgress);

                if (phase.Progress >= 1f && TryCompletePhase(project, phase, phaseDef))
                {
                    anyPhaseCompletedToday = true;
                }
            }

            if (anyPhaseCompletedToday)
            {
                UnlockEligiblePhases(project);
            }

            MaybePayMilestone(project);
            CompleteProjectIfAllPhasesDone(project, currentDate);
        }

        /// <summary>Called by SubcontractorSystem's scheduled callback once an awarded subcontract resolves.</summary>
        private void CompleteSubcontractedPhase(string projectId, string phaseId)
        {
            var project = ActiveProjects.FirstOrDefault(p => p.Id == projectId);
            var phase = project != null ? project.Phases.FirstOrDefault(ph => ph.PhaseId == phaseId) : null;
            if (project == null || phase == null || phase.Status != PhaseStatus.InProgress) return;

            phase.Progress = 1f;
            var phaseDef = _content.GetPhase(phaseId);
            if (phaseDef == null) return;

            if (!TryCompletePhase(project, phase, phaseDef)) return; // won't happen for subcontracted phases (see TryCompletePhase), but keep the guard consistent

            UnlockEligiblePhases(project);
            MaybePayMilestone(project);

            var currentDate = _timeManager != null ? _timeManager.CurrentDate : DateTime.UtcNow;
            CompleteProjectIfAllPhasesDone(project, currentDate);
        }

        /// <summary>
        /// Scores the phase's quality, applies the inspection gate if this
        /// phase requires one, and completes it if it passes (or isn't
        /// gated at all). Returns false without completing the phase if it
        /// failed an inspection - it stays InProgress at reduced progress.
        /// </summary>
        private bool TryCompletePhase(ProjectRuntimeData project, PhaseRuntimeData phase, ConstructionPhaseDefinition phaseDef)
        {
            float quality = ComputePhaseQuality(phase, phaseDef);

            if (!phase.IsSubcontracted && phaseDef.RequiresInspection && quality < InspectionPassQuality)
            {
                FailInspection(project, phase, quality);
                return false;
            }

            CompletePhase(project, phase, quality, phaseDef.RequiresInspection);
            return true;
        }

        private float ComputePhaseQuality(PhaseRuntimeData phase, ConstructionPhaseDefinition phaseDef)
        {
            if (phase.IsSubcontracted)
            {
                var subDef = _content.GetSubcontractor(phase.AssignedSubcontractorId);
                return subDef != null ? subDef.BaseQuality : 0.6f;
            }

            float averageCrewProductivity = ComputeAverageCrewProductivity(phase);
            float equipmentFactor = ComputeEquipmentFactor(phase, phaseDef);
            float materialFactor = ComputeMaterialFactor(phaseDef);

            return QualityScoring.BlendInHouseQuality(averageCrewProductivity, equipmentFactor, materialFactor);
        }

        private float ComputeAverageCrewProductivity(PhaseRuntimeData phase)
        {
            if (phase.AssignedCrewIds.Count == 0) return 0f;

            float total = 0f;
            int counted = 0;
            foreach (var crewId in phase.AssignedCrewIds)
            {
                float productivity = _crewSystem.GetCrewProductivity(crewId);
                if (productivity > 0f)
                {
                    total += productivity;
                    counted++;
                }
            }

            return counted > 0 ? total / counted : 0f;
        }

        private void FailInspection(ProjectRuntimeData project, PhaseRuntimeData phase, float quality)
        {
            phase.Progress = Math.Max(0f, phase.Progress - InspectionReworkSetback);

            var phaseDef = _content.GetPhase(phase.PhaseId);
            double reworkCost = (phaseDef != null ? phaseDef.BaseLaborCost : 2000) * 0.15;

            _companySystem.TrySpendCash(reworkCost, "Inspection failed - rework: " + project.DisplayName + " (" + phase.DisplayName + ")");
            _companySystem.AdjustReputation(-3f, "Failed inspection: " + project.DisplayName);
            project.CostIncurredToDate += reworkCost;

            EventBus.Publish(new InspectionFailedEvent(project.Id, phase.PhaseId, quality));
        }

        private void CompleteProjectIfAllPhasesDone(ProjectRuntimeData project, DateTime currentDate)
        {
            if (project.Phases.Count > 0 && project.Phases.All(p => p.Status == PhaseStatus.Completed))
            {
                CompleteProject(project, currentDate);
            }
        }

        private float ComputeCrewFactor(PhaseRuntimeData phase)
        {
            if (phase.AssignedCrewIds.Count == 0) return 0f;

            float total = 0f;
            int counted = 0;
            foreach (var crewId in phase.AssignedCrewIds)
            {
                float productivity = _crewSystem.GetCrewProductivity(crewId);
                if (productivity > 0f)
                {
                    total += productivity;
                    counted++;
                }
            }

            return counted > 0 ? total : 0f;
        }

        private float ComputeEquipmentFactor(PhaseRuntimeData phase, ConstructionPhaseDefinition phaseDef)
        {
            if (phaseDef.RequiredEquipment == null || phaseDef.RequiredEquipment.Length == 0) return 1f;
            if (phase.AssignedEquipmentIds.Count == 0) return 0.5f; // required but missing - slow, not zero; matches "productivity suffers" rather than a hard stop

            float total = 0f;
            int counted = 0;
            foreach (var equipmentId in phase.AssignedEquipmentIds)
            {
                float efficiency = _equipmentSystem.GetEfficiency(equipmentId);
                if (efficiency > 0f)
                {
                    total += efficiency;
                    counted++;
                }
            }

            return counted > 0 ? total / counted : 0.5f;
        }

        private float ComputeMaterialFactor(ConstructionPhaseDefinition phaseDef)
        {
            if (phaseDef.RequiredMaterials == null || phaseDef.RequiredMaterials.Length == 0) return 1f;

            float worst = 1f;
            foreach (var req in phaseDef.RequiredMaterials)
            {
                double available = _materialSystem.GetAvailable(req.Type);
                float ratio = req.Quantity > 0 ? (float)Math.Min(1.0, available / req.Quantity) : 1f;
                if (ratio < worst) worst = ratio;
            }

            return worst;
        }

        private void ConsumeMaterialsForProgress(ConstructionPhaseDefinition phaseDef, double dailyProgress)
        {
            if (phaseDef.RequiredMaterials == null) return;

            foreach (var req in phaseDef.RequiredMaterials)
            {
                double amountUsed = req.Quantity * dailyProgress;
                if (amountUsed > 0)
                {
                    _materialSystem.ConsumeMaterial(req.Type, amountUsed);
                }
            }
        }

        private void CompletePhase(ProjectRuntimeData project, PhaseRuntimeData phase, float quality, bool wasInspected)
        {
            phase.Status = PhaseStatus.Completed;
            phase.QualityScore = quality;

            foreach (var crewId in phase.AssignedCrewIds)
            {
                _crewSystem.ReleaseCrew(crewId);
            }

            foreach (var equipmentId in phase.AssignedEquipmentIds)
            {
                _equipmentSystem.ReleaseEquipment(equipmentId);
            }

            UpdateProjectQuality(project);

            if (wasInspected)
            {
                EventBus.Publish(new InspectionPassedEvent(project.Id, phase.PhaseId, quality));
            }

            EventBus.Publish(new PhaseCompletedEvent(project.Id, phase.PhaseId));
        }

        /// <summary>Recomputes the project's overall quality as the QualityImpactWeight-weighted average of its completed phases (spec section 20).</summary>
        private void UpdateProjectQuality(ProjectRuntimeData project)
        {
            float weightedSum = 0f;
            float totalWeight = 0f;

            foreach (var phase in project.Phases.Where(p => p.Status == PhaseStatus.Completed))
            {
                var phaseDef = _content.GetPhase(phase.PhaseId);
                float weight = phaseDef != null ? phaseDef.QualityImpactWeight : 0.1f;
                weightedSum += phase.QualityScore * weight;
                totalWeight += weight;
            }

            project.QualityScore = totalWeight > 0f ? weightedSum / totalWeight : 0f;
        }

        private void MaybePayMilestone(ProjectRuntimeData project)
        {
            if (project.MilestonePaid || project.Phases.Count == 0) return;

            float overallProgress = (float)project.Phases.Count(p => p.Status == PhaseStatus.Completed) / project.Phases.Count;
            if (overallProgress < 0.5f) return;

            double milestonePayment = project.ContractValue * 0.50;
            _companySystem.AddCash(milestonePayment, "Progress payment: " + project.DisplayName);
            project.CashReceivedToDate += milestonePayment;
            project.MilestonePaid = true;
        }

        private void CompleteProject(ProjectRuntimeData project, DateTime currentDate)
        {
            project.Status = ProjectStatus.Completed;
            project.CompletedDateIso = currentDate.ToString("o");

            var template = _content.GetProjectTemplate(project.Type);
            QualityLevel achievedLevel = QualityScoring.ScoreToLevel(project.QualityScore);
            QualityLevel requiredLevel = template != null ? template.RequiredQuality : QualityLevel.Average;

            double qualityPaymentAdjustment = 0;
            float qualityReputationDelta = 0f;

            if (achievedLevel > requiredLevel)
            {
                qualityPaymentAdjustment = project.ContractValue * 0.02; // client happy with the extra quality
                qualityReputationDelta = 4f;
            }
            else if (achievedLevel < requiredLevel)
            {
                qualityPaymentAdjustment = -project.ContractValue * 0.05; // client withholds some payment
                qualityReputationDelta = -6f;
            }

            double baseRemaining = project.ContractValue - project.CashReceivedToDate;
            double finalPayment = Math.Max(0, baseRemaining + qualityPaymentAdjustment);
            if (finalPayment > 0)
            {
                _companySystem.AddCash(finalPayment, "Final payment: " + project.DisplayName);
                project.CashReceivedToDate += finalPayment;
            }

            bool onTime = !DateTime.TryParse(project.DeadlineIso, out var deadline) || currentDate <= deadline;
            _companySystem.AdjustReputation(onTime ? 8f : -8f, onTime ? "Completed on time: " + project.DisplayName : "Completed late: " + project.DisplayName);

            if (qualityReputationDelta != 0f)
            {
                _companySystem.AdjustReputation(qualityReputationDelta, qualityReputationDelta > 0
                    ? "Exceeded quality expectations: " + project.DisplayName
                    : "Fell short of quality expectations: " + project.DisplayName);
            }

            if (_clientSystem != null && !string.IsNullOrEmpty(project.ClientId))
            {
                float relationshipDelta = onTime ? 5f : -5f;
                relationshipDelta += qualityReputationDelta > 0 ? 3f : (qualityReputationDelta < 0 ? -4f : 0f);
                _clientSystem.AdjustRelationship(project.ClientId, relationshipDelta, "Project completed: " + project.DisplayName);
            }

            double finalProfit = project.ContractValue - project.CostIncurredToDate;

            ActiveProjects.Remove(project);
            CompletedProjects.Add(project);

            EventBus.Publish(new ProjectCompletedEvent(project.Id, onTime, finalProfit));
        }

        public void CaptureState(out List<ProjectRuntimeData> opportunities, out List<ProjectRuntimeData> active, out List<ProjectRuntimeData> completed)
        {
            opportunities = AvailableOpportunities;
            active = ActiveProjects;
            completed = CompletedProjects;
        }

        public void RestoreState(List<ProjectRuntimeData> opportunities, List<ProjectRuntimeData> active, List<ProjectRuntimeData> completed)
        {
            AvailableOpportunities = opportunities ?? new List<ProjectRuntimeData>();
            ActiveProjects = active ?? new List<ProjectRuntimeData>();
            CompletedProjects = completed ?? new List<ProjectRuntimeData>();
        }
    }
}
