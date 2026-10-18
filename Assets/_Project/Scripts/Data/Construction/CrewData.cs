using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Construction
{
    [Serializable, DataContract]
    public sealed class CrewData : SaveDataBase
    {
        public string CurrentProjectId => Members.FirstOrDefault()?.CurrentProjectId;
        public string CurrentTaskId => Members.FirstOrDefault()?.CurrentTaskId;
        [DataMember] public bool UseShiftPolicy,WorkWeekends;
        [DataMember] public int ShiftStartHour=8,ShiftHours=8;
        [DataMember] public double OvertimeMultiplier=1;
        public bool IsOnShift(long hour)=>!UseShiftPolicy || (WorkWeekends || (hour/24)%7<5) && hour%24>=ShiftStartHour && hour%24<ShiftStartHour+ShiftHours;
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public List<EmployeeData> Members = new List<EmployeeData>();
        public double ResearchEfficiencyMultiplier {get;set;}=1;
        public double Productivity => Members.Sum(x=>x.Productivity);
        public double CostPerHour => Members.Sum(x=>x.WagePerHour);
        public double Morale => Members.Count==0 ? 0 : Members.Average(x=>x.Morale);
        public void Validate()
        {
            ConstructionValidation.Text(Id,nameof(Id)); ConstructionValidation.Text(Name,nameof(Name));
            if(Members==null || Members.Count==0) throw new ArgumentException("A crew requires members.");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var worker in Members) { if(worker==null) throw new ArgumentException("Null worker."); worker.Validate(); if(!ids.Add(worker.Id)) throw new ArgumentException("Duplicate worker in crew."); }
        }
        public override void Migrate() { if(SchemaVersion!=1) { base.Migrate(); return; } Validate(); SchemaVersion=2; }
    }
    [Serializable]
    public sealed class CrewEfficiencyConfiguration
    {
        public double UnmatchedTradeMultiplier = .5;
        public double SkillFloor = .5;
        public double ExperienceBonusPerYear = .02;
        public double MaximumExperienceBonus = .3;
        public double NoSupervisorMultiplier = .8;
        public double SupervisorSkillBonus = .2;
        public double MaximumFatiguePenalty = .8;
        public void Validate()
        {
            ConstructionValidation.Unit(UnmatchedTradeMultiplier,nameof(UnmatchedTradeMultiplier)); ConstructionValidation.Unit(SkillFloor,nameof(SkillFloor));
            ConstructionValidation.NonNegative(ExperienceBonusPerYear,nameof(ExperienceBonusPerYear)); ConstructionValidation.NonNegative(MaximumExperienceBonus,nameof(MaximumExperienceBonus));
            ConstructionValidation.NonNegative(NoSupervisorMultiplier,nameof(NoSupervisorMultiplier)); ConstructionValidation.NonNegative(SupervisorSkillBonus,nameof(SupervisorSkillBonus));
            ConstructionValidation.Unit(MaximumFatiguePenalty,nameof(MaximumFatiguePenalty));
        }
    }
}
