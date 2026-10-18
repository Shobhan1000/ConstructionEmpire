using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Risk
{
    public sealed class RandomEventSystem : IRegisterableSystem
    {
        private readonly ManagementSession session;private readonly SiteConditionsSystem sites;private readonly RiskSettings settings;private readonly SeededRandom random;private readonly Func<ProjectData,ProjectRiskAssessment> assess;
        private readonly HashSet<string> inspectionsPassed=new HashSet<string>(),requiredReinspections=new HashSet<string>();private int sequence;
        public RandomEventSystem(ManagementSession session,SiteConditionsSystem sites,RiskSettings settings,Func<ProjectData,ProjectRiskAssessment> assess)
        {this.session=session;this.sites=sites;this.settings=settings;this.assess=assess;random=session.Simulation.Random;}
        public static double Probability(double hourlyRate,double risk,double vulnerability,double hours,RiskSettings settings)
        {ConstructionValidation.NonNegative(hourlyRate,"Rate");ConstructionValidation.Unit(risk,"Risk");ConstructionValidation.Unit(vulnerability,"Vulnerability");ConstructionValidation.NonNegative(hours,"Hours");return 1-Math.Exp(-hourlyRate*(settings.MinimumRiskFrequency+settings.RiskFrequencyMultiplier*risk)*vulnerability*hours);}
        private bool Roll(double rate,double risk,double vulnerability,double hours)=>random.NextDouble()<Probability(rate,risk,vulnerability,hours,settings);
        public void OnTick(TickContext context)
        {
            foreach(var worker in session.Workforce.Workers.OrderBy(x=>x.Id,StringComparer.Ordinal))
            {
                bool working=worker.CurrentProjectId!=null && worker.AbsentUntilHour<context.TotalHours && session.Expansion.Crews.Where(c=>c.Members.Contains(worker)).All(c=>c.IsOnShift(context.TotalHours));
                worker.Fatigue=Clamp(worker.Fatigue+(working?settings.FatiguePerWorkHour:-settings.RestPerHour)*context.ElapsedGameHours);
                if(worker.AbsentUntilHour>0 && worker.AbsentUntilHour<context.TotalHours){worker.AbsentUntilHour=0;GameEvents.Publish(new ResourcesChangedEvent(worker.CurrentProjectId));}
            }
            foreach(var p in session.Projects.Where(x=>x.Status==ProjectStatus.Active).OrderBy(x=>x.Id,StringComparer.Ordinal))
            {
                double risk=assess(p).Score;
                foreach(var machine in session.Workforce.Equipment.Where(x=>x.CurrentProjectId==p.Id && x.Status==EquipmentStatus.Working).OrderBy(x=>x.Id,StringComparer.Ordinal))
                {
                    var assignedCrew=session.Expansion.Crews.FirstOrDefault(c=>c.CurrentProjectId==p.Id && c.CurrentTaskId==machine.CurrentTaskId);
                    if(assignedCrew!=null && !assignedCrew.IsOnShift(context.TotalHours))continue;
                    machine.Condition=Clamp(machine.Condition-settings.EquipmentWearPerHour*context.ElapsedGameHours);
                    if(machine.Condition==0 || Roll(settings.BreakdownRate*machine.BreakdownRateMultiplier,risk,1-machine.Condition,context.ElapsedGameHours))Breakdown(p,machine);
                }
                var workers=session.Workforce.Workers.Where(x=>x.CurrentProjectId==p.Id && x.AbsentUntilHour<context.TotalHours && session.Expansion.Crews.Where(c=>c.Members.Contains(x)).All(c=>c.IsOnShift(context.TotalHours))).OrderBy(x=>x.Id,StringComparer.Ordinal).ToList();
                foreach(var worker in workers)if(Roll(settings.AbsenceRate,risk,Clamp((worker.Fatigue+1-worker.Morale+1-worker.Reliability)/3),context.ElapsedGameHours))Absent(p,worker,context.TotalHours);
                if(workers.Count>0 && context.TotalHours>sites.StoppedUntil(p.Id))
                {
                    double supervisor=workers.Where(x=>x.WorkerType==WorkerType.SiteSupervisor).Select(x=>x.Skill).DefaultIfEmpty(0).Max();
                    double condition=session.Workforce.Equipment.Where(x=>x.CurrentProjectId==p.Id).Select(x=>x.Condition).DefaultIfEmpty(1).Average();
                    double vulnerability=Clamp((1-workers.Average(x=>x.Safety)+workers.Average(x=>x.Fatigue)+1-p.Site.SafetyTraining+1-supervisor+1-condition+p.Site.Difficulty)/6);
                    if(Roll(settings.SafetyRate,risk,vulnerability,context.ElapsedGameHours))SafetyIncident(p,context.TotalHours);
                }
                if(!p.Site.GroundConditionDiscovered && session.Phases(p.Id).Any(x=>settings.GroundPhaseIds.Contains(x.Id) && x.Progress>0 && !x.IsComplete) && Roll(settings.GroundDiscoveryRate,risk,p.Site.Soil,context.ElapsedGameHours))DiscoverGround(p,context.TotalHours);
            }
        }
        public void Breakdown(ProjectData p,EquipmentRuntimeData machine)
        {
            Active(p);if(machine.CurrentProjectId!=p.Id || machine.Status!=EquipmentStatus.Working)throw new InvalidOperationException("Choose working equipment assigned to this active project.");
            machine.Status=EquipmentStatus.Broken;GameEvents.Publish(new EquipmentBreakdownEvent(machine.Id,p.Id,machine.Name+" is unavailable until repaired. Condition: "+machine.Condition.ToString("P0")));Changed(p,"Equipment stopped.");
        }
        public void Repair(EquipmentRuntimeData machine)
        {
            if(machine.Status!=EquipmentStatus.Broken && machine.Condition>=settings.RepairCondition)throw new InvalidOperationException("This machine does not need maintenance.");
            var repairReference=Expense(machine.CurrentProjectId,machine.MaintenanceCost,FinancialCategory.Maintenance,"Repair: "+machine.Name,session.Simulation.Time.TotalHours);
            if(machine.Status==EquipmentStatus.Broken)session.Finance.Insurance.Claim(ConstructionEmpire.Systems.Finance.InsuranceType.Equipment,repairReference,machine.CurrentProjectId,(decimal)machine.MaintenanceCost);
            machine.Condition=settings.RepairCondition;machine.Status=machine.CurrentProjectId==null?EquipmentStatus.Available:EquipmentStatus.Working;
            GameEvents.Publish(new ResourcesChangedEvent(machine.CurrentProjectId));if(machine.CurrentProjectId!=null)GameEvents.Publish(new RiskStateChangedEvent(machine.CurrentProjectId,"Equipment repaired."));
        }
        public void Absent(ProjectData p,EmployeeData worker,long hour)
        {
            Active(p);if(worker.CurrentProjectId!=p.Id || worker.AbsentUntilHour>hour)throw new InvalidOperationException("Choose a present worker assigned to this project.");
            worker.AbsentUntilHour=checked(hour+settings.AbsenceHours);GameEvents.Publish(new WorkerAbsentEvent(worker.Id,p.Id,settings.AbsenceHours,worker.Name+" is absent; remaining crew capacity is used."));Changed(p,"Crew capacity reduced.");
        }
        public void SafetyIncident(ProjectData p,long hour)
        {
            Active(p);sites.Stop(p.Id,hour,settings.SafetyStopHours);
            var incidentReference=Expense(p.Id,settings.SafetyInvestigationCost,FinancialCategory.Other,"Safety investigation",hour);
            Expense(p.Id,settings.SafetyInsuranceExcess,FinancialCategory.Insurance,"Uninsured safety incident excess",hour);
            session.Finance.Insurance.Claim(ConstructionEmpire.Systems.Finance.InsuranceType.GeneralLiability,incidentReference,p.Id,(decimal)settings.SafetyInvestigationCost/2);
            session.Finance.Insurance.Claim(ConstructionEmpire.Systems.Finance.InsuranceType.WorkersCompensation,incidentReference,p.Id,(decimal)settings.SafetyInvestigationCost/2);
            var company=session.Simulation.Company;company.SetReputation((float)Math.Max(0,company.Reputation-settings.SafetyReputationLoss),"Site safety incident");
            GameEvents.Publish(new SafetyIncidentEvent(p.Id,"incident-"+(++sequence),"Work stopped for a safety investigation. Review training, fatigue, supervision and equipment before resuming."));Changed(p,"Safety work stoppage.");
        }
        public void DiscoverGround(ProjectData p,long hour)
        {Active(p);if(p.Site.GroundConditionDiscovered)throw new InvalidOperationException("Ground condition already investigated.");sites.DiscoverPoorGround(p,hour);var groundReference=Expense(p.Id,settings.GroundRemediationCost,FinancialCategory.Other,"Ground investigation and remediation",hour);session.Finance.Insurance.Claim(ConstructionEmpire.Systems.Finance.InsuranceType.Project,groundReference,p.Id,(decimal)settings.GroundRemediationCost);}
        public bool Inspect(ProjectData p,ConstructionPhaseRuntimeData phase,long hour)
        {
            string key=p.Id+":"+phase.Id;
            if(!settings.InspectionPhaseIds.Contains(phase.Id) && !requiredReinspections.Contains(key))return true;
            if(inspectionsPassed.Contains(key))return true;
            var crew=session.Workforce.Workers.Where(x=>x.CurrentProjectId==p.Id && x.AbsentUntilHour<hour).ToList();
            var award=session.Subcontractors.Awards.FirstOrDefault(x=>x.Quote.ProjectId==p.Id && x.Quote.PhaseId==phase.Id);
            double skill=award?.Quote.Quality??(crew.Count==0?0:crew.Average(x=>x.Skill));
            // A phase reaches this gate only after its required materials have been consumed by the engine.
            double vulnerability=Clamp((1-p.Site.Quality+1-p.Site.Compliance+1-skill)/3);
            double failure=Clamp((settings.InspectionBaseFailure+settings.InspectionVulnerabilityWeight*vulnerability)*(settings.MinimumRiskFrequency+settings.RiskFrequencyMultiplier*assess(p).Score));
            if(random.NextDouble()<failure){FailInspection(p,phase,hour);return false;}
            inspectionsPassed.Add(key);requiredReinspections.Remove(key);GameEvents.Publish(new InspectionPassedEvent(p.Id,phase.Id));return true;
        }
        public void FailInspection(ProjectData p,ConstructionPhaseRuntimeData phase,long hour)
        {
            Active(p);if(phase.IsComplete)throw new InvalidOperationException("A completed phase cannot be reopened by this debug event.");
            inspectionsPassed.Remove(p.Id+":"+phase.Id);requiredReinspections.Add(p.Id+":"+phase.Id);phase.ReworkHoursRemaining+=settings.InspectionReworkHours;
            p.Site.Quality=Clamp(p.Site.Quality-settings.InspectionQualityLoss);sites.Stop(p.Id,hour,settings.InspectionStopHours);
            Expense(p.Id,settings.InspectionCost,FinancialCategory.Other,"Inspection remediation: "+phase.Name,hour);
            GameEvents.Publish(new InspectionFailedEvent(p.Id,phase.Id,"Rework required for "+phase.Name+"; additional cost and working hours have been added. Completion remains blocked until reinspection passes."));Changed(p,"Inspection rework pending.");
        }
        public void TrainCrew(ProjectData p)
        {
            Active(p);var crew=session.Workforce.Workers.Where(x=>x.CurrentProjectId==p.Id).ToList();if(crew.Count==0)throw new InvalidOperationException("Assign a crew before training.");
            Expense(p.Id,crew.Count*settings.TrainingCostPerWorker,FinancialCategory.Labor,"Crew safety training",session.Simulation.Time.TotalHours);
            foreach(var w in crew)w.Safety=Clamp(w.Safety+settings.TrainingImprovement);p.Site.SafetyTraining=Clamp(p.Site.SafetyTraining+settings.TrainingImprovement);Changed(p,"Safety training completed.");
        }
        private string Expense(string project,double value,FinancialCategory category,string description,long hour)=>session.Finance.AccrueExpense(project,(decimal)value,category,"risk-expense-"+(++sequence),description,hour);
        private static void Active(ProjectData p){if(p==null || p.Status!=ProjectStatus.Active)throw new InvalidOperationException("An active project is required.");}
        private static double Clamp(double x)=>Math.Max(0,Math.Min(1,x));
        private static void Changed(ProjectData p,string reason){GameEvents.Publish(new RiskStateChangedEvent(p.Id,reason));GameEvents.Publish(new ResourcesChangedEvent(p.Id));}
    }
}
