using System;
using System.Linq;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Systems.Construction
{
    public readonly struct CrewEfficiencyBreakdown
    {
        public double SkillCompatibility { get; }
        public double Experience { get; }
        public double SupervisorQuality { get; }
        public double Fatigue { get; }
        public double EquipmentAvailability { get; }
        public double MaterialAvailability { get; }
        // Resource terms are exposed separately so the Section 54 chain does not count them twice.
        public double ResearchModifier {get;}
        public double LaborEfficiency => SkillCompatibility * Experience * SupervisorQuality * Fatigue * ResearchModifier;
        public double Efficiency => LaborEfficiency * EquipmentAvailability * MaterialAvailability;
        public CrewEfficiencyBreakdown(double skill,double experience,double supervisor,double fatigue,double equipment,double material,double research=1)
        { ResearchModifier=research; SkillCompatibility=skill; Experience=experience; SupervisorQuality=supervisor; Fatigue=fatigue; EquipmentAvailability=equipment; MaterialAvailability=material; }
    }
    public static class CrewEfficiencyCalculator
    {
        public static CrewEfficiencyBreakdown Calculate(CrewData crew,ConstructionPhaseRuntimeData phase,CrewEfficiencyConfiguration config,double equipmentAvailability=1,double materialAvailability=1)
        {
            if(crew==null || phase==null || config==null) throw new ArgumentNullException();
            crew.Validate(); phase.Validate(); config.Validate();
            ConstructionValidation.NonNegative(equipmentAvailability,nameof(equipmentAvailability)); ConstructionValidation.Unit(materialAvailability,nameof(materialAvailability));
            double skill=crew.Members.Average(w=>(config.SkillFloor+(1-config.SkillFloor)*w.Skill) *
                (phase.CompatibleTrades.Count==0 || phase.CompatibleTrades.Contains(w.WorkerType) || w.WorkerType==WorkerType.SiteSupervisor ? 1 : config.UnmatchedTradeMultiplier));
            double experience=1+Math.Min(config.MaximumExperienceBonus,crew.Members.Average(w=>w.Experience)*config.ExperienceBonusPerYear);
            var supervisors=crew.Members.Where(w=>w.WorkerType==WorkerType.SiteSupervisor).ToList();
            double supervisor=supervisors.Count==0 ? config.NoSupervisorMultiplier : 1+supervisors.Max(w=>w.Skill)*config.SupervisorSkillBonus;
            double fatigue=1-crew.Members.Average(w=>w.Fatigue)*config.MaximumFatiguePenalty;
            return new CrewEfficiencyBreakdown(skill,experience,supervisor,fatigue,equipmentAvailability,materialAvailability,crew.ResearchEfficiencyMultiplier);
        }
    }
}
