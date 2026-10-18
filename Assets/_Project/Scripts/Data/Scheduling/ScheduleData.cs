using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Scheduling
{
    [Serializable,DataContract] public sealed class PhaseScheduleData
    {
        [DataMember] public string PhaseId,Name;
        [DataMember] public double PlannedStart,PlannedCompletion,ForecastStart,ForecastCompletion,TotalFloat,CurrentDelay,Progress;
        [DataMember] public double? ActualStart,ActualCompletion;
        [DataMember] public bool IsCriticalPath;
        public PhaseScheduleData Copy()=>(PhaseScheduleData)MemberwiseClone();
    }
    [Serializable,DataContract] public sealed class ScheduleData : SaveDataBase
    {
        [DataMember] public string ProjectId;
        [DataMember] public double PlannedStart,PlannedCompletion,ForecastCompletion;
        [DataMember] public List<PhaseScheduleData> Phases=new List<PhaseScheduleData>();
        public ScheduleData Copy(){var copy=(ScheduleData)MemberwiseClone();copy.Phases=Phases.ConvertAll(x=>x.Copy());return copy;}
        public override void Migrate(){if(SchemaVersion!=1){base.Migrate();return;}SchemaVersion=2;}
    }
    public enum BookingStatus { Pending,Active,Blocked,Complete,Expired,Released }
    public sealed class ResourceBookingData
    {
        public string Id {get;}public string ProjectId {get;}public string PhaseId {get;}public string ResourceName {get;}
        public long StartHour {get;}public long EndHour {get;}public BookingStatus Status {get;set;}public string Conflict {get;set;}
        public ResourceBookingData(string id,string project,string phase,string resource,long start,long end){Id=id;ProjectId=project;PhaseId=phase;ResourceName=resource;StartHour=start;EndHour=end;}
    }
    public readonly struct ScheduleChangedEvent
    {public string ProjectId {get;}public ScheduleChangedEvent(string project){ProjectId=project;}}
    public readonly struct ResourceBookingConflictEvent
    {public string ProjectId {get;}public string PhaseId {get;}public string Description {get;}public ResourceBookingConflictEvent(string project,string phase,string description){ProjectId=project;PhaseId=phase;Description=description;}}
}
