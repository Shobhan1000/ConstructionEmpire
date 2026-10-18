using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Construction
{
    public enum WorkerType { GeneralLaborer, Carpenter, Mason, Electrician, Plumber, HeavyEquipmentOperator, Welder, Painter, SiteSupervisor, ProjectManager }
    [Serializable, DataContract]
    public sealed class EmployeeData : SaveDataBase
    {
        [DataMember] public long AbsentUntilHour;
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public int Age = 25;
        [DataMember] public WorkerType WorkerType;
        // Ratings use 0..1; productivity is equivalent labor-hours produced per game hour.
        [DataMember] public double Skill;
        [DataMember] public double Experience;
        [DataMember] public double Productivity;
        [DataMember] public double Reliability;
        [DataMember] public double Safety;
        [DataMember] public double WagePerHour;
        [DataMember] public double Morale;
        [DataMember] public double Fatigue;
        [DataMember] public List<string> Certifications = new List<string>();
        [DataMember] public string Specialty;
        [DataMember] public string CurrentProjectId;
        [DataMember] public string CurrentTaskId;
        public void Validate()
        {
            ConstructionValidation.Text(Id,nameof(Id)); ConstructionValidation.Text(Name,nameof(Name));
            if(AbsentUntilHour<0)throw new ArgumentException("Absence end must be non-negative.");
            if (Age < 18 || Age > 100) throw new ArgumentException("Worker age must be 18..100.");
            if (!Enum.IsDefined(typeof(WorkerType),WorkerType)) throw new ArgumentException("Unknown worker type.");
            ConstructionValidation.Unit(Skill,nameof(Skill)); ConstructionValidation.NonNegative(Experience,nameof(Experience));
            ConstructionValidation.NonNegative(Productivity,nameof(Productivity)); ConstructionValidation.Unit(Reliability,nameof(Reliability));
            ConstructionValidation.Unit(Safety,nameof(Safety)); ConstructionValidation.NonNegative(WagePerHour,nameof(WagePerHour));
            ConstructionValidation.Unit(Morale,nameof(Morale)); ConstructionValidation.Unit(Fatigue,nameof(Fatigue));
            if (Certifications == null) throw new ArgumentException("Certifications must be a list.");
            if (string.IsNullOrEmpty(CurrentProjectId) != string.IsNullOrEmpty(CurrentTaskId)) throw new ArgumentException("Assignment needs both project and task IDs.");
        }
        public EmployeeData Copy() { var copy=(EmployeeData)MemberwiseClone(); copy.Certifications=new List<string>(Certifications); return copy; }
        public override void Migrate() { if (SchemaVersion!=1) { base.Migrate(); return; } Validate(); SchemaVersion=2; }
    }
}
