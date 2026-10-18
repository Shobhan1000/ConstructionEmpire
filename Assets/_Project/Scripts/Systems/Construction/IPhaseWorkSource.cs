using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Systems.Construction
{
    /// <summary>Both employees and subcontractors supply work through this contract. The engine owns progress and resource consumption.</summary>
    public interface IPhaseWorkSource
    {
        string Id {get;} IEnumerable<string> WorkerIds {get;} bool IsClaimed {get;} double WagePerHour {get;}
        void Validate();bool CanPerform(ConstructionPhaseRuntimeData phase);bool IsAvailable(long hour);
        double BaseProgressPerHour(ConstructionPhaseRuntimeData phase);double LaborEfficiency(ConstructionPhaseRuntimeData phase,CrewEfficiencyConfiguration configuration);
        void Claim(string projectId,string phaseId);void Release();
    }
    public sealed class InternalCrewWorkSource : IPhaseWorkSource
    {
        private readonly CrewData crew;
        private long currentHour;
        private CrewData Present=>new CrewData {Id=crew.Id,Name=crew.Name,ResearchEfficiencyMultiplier=crew.ResearchEfficiencyMultiplier,Members=crew.Members.Where(w=>w.AbsentUntilHour==0 || w.AbsentUntilHour<currentHour).ToList()};
        public InternalCrewWorkSource(CrewData crew){this.crew=crew??throw new ArgumentNullException(nameof(crew));}
        public string Id=>crew.Id;public IEnumerable<string> WorkerIds=>crew.Members.Select(x=>x.Id);public bool IsClaimed=>crew.Members.Any(x=>!string.IsNullOrEmpty(x.CurrentProjectId));public double WagePerHour=>Present.CostPerHour*crew.OvertimeMultiplier;
        public void Validate()=>crew.Validate();public bool CanPerform(ConstructionPhaseRuntimeData phase)=>!phase.RequiresSubcontractor;public bool IsAvailable(long hour){currentHour=hour;return crew.IsOnShift(hour) && Present.Members.Count>0;}
        public double BaseProgressPerHour(ConstructionPhaseRuntimeData phase)=>Math.Min(100/phase.DurationHours,100*Present.Productivity/phase.LaborHours);
        public double LaborEfficiency(ConstructionPhaseRuntimeData phase,CrewEfficiencyConfiguration configuration)=>CrewEfficiencyCalculator.Calculate(Present,phase,configuration).LaborEfficiency;
        public void Claim(string projectId,string phaseId){foreach(var worker in crew.Members){worker.CurrentProjectId=projectId;worker.CurrentTaskId=phaseId;}}
        public void Release(){foreach(var worker in crew.Members){worker.CurrentProjectId=null;worker.CurrentTaskId=null;}}
    }
}
