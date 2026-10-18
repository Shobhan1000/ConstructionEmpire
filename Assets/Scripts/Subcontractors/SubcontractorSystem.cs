using System;
using System.Collections.Generic;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Subcontractors
{
    /// <summary>
    /// Quotes and awards subcontracts for phases that have a
    /// RelevantSpecialty (spec section 18). A subcontracted phase resolves
    /// on its own schedule via SimulationManager rather than the daily
    /// crew-driven tick - ConstructionProjectSystem owns the resulting
    /// phase-state transitions; this system only knows about quotes,
    /// pricing, and the reliability roll.
    /// </summary>
    public class SubcontractorSystem
    {
        private readonly ContentLibrary _content;
        private readonly CompanySystem _companySystem;
        private readonly SimulationManager _simulationManager;
        private readonly SeededRandom _random;

        public SubcontractorSystem(ContentLibrary content, CompanySystem companySystem, SimulationManager simulationManager, SeededRandom random)
        {
            _content = content;
            _companySystem = companySystem;
            _simulationManager = simulationManager;
            _random = random;
        }

        public List<SubcontractorQuote> GetQuotes(SubcontractorSpecialty specialty, ConstructionPhaseDefinition phaseDef)
        {
            var quotes = new List<SubcontractorQuote>();
            if (specialty == SubcontractorSpecialty.None || phaseDef == null) return quotes;

            foreach (var def in _content.GetSubcontractors(specialty))
            {
                quotes.Add(new SubcontractorQuote
                {
                    SubcontractorId = def.Id,
                    CompanyName = def.CompanyName,
                    Price = phaseDef.BaseLaborCost * def.BaseCostMultiplier,
                    EstimatedDurationDays = Math.Max(1, (int)Math.Round(phaseDef.BaseDurationDays * def.SpeedFactor)),
                    Reliability = def.BaseReliability,
                    Quality = def.BaseQuality
                });
            }

            return quotes;
        }

        /// <summary>
        /// Charges the price immediately, rolls reliability, and schedules
        /// onComplete(onTime) at the resolved completion date. Returns
        /// false (nothing charged or scheduled) if the subcontractor or
        /// phase can't be found, or funds are short.
        /// </summary>
        public bool AwardSubcontract(string subcontractorId, ConstructionPhaseDefinition phaseDef, DateTime currentDate, string projectDisplayName, Action<bool> onComplete)
        {
            var def = _content.GetSubcontractor(subcontractorId);
            if (def == null || phaseDef == null) return false;

            double price = phaseDef.BaseLaborCost * def.BaseCostMultiplier;
            if (!_companySystem.TrySpendCash(price, "Subcontract (" + def.CompanyName + "): " + projectDisplayName)) return false;

            int estimatedDays = Math.Max(1, (int)Math.Round(phaseDef.BaseDurationDays * def.SpeedFactor));
            bool onTime = _random.NextBool(def.BaseReliability);

            int actualDays = onTime
                ? estimatedDays
                : estimatedDays + Math.Max(1, (int)Math.Round(estimatedDays * _random.NextFloat(0.3f, 0.8f)));

            var completionDate = currentDate.AddDays(actualDays);
            _simulationManager.ScheduleEvent(completionDate, () => onComplete?.Invoke(onTime));

            return true;
        }
    }
}
