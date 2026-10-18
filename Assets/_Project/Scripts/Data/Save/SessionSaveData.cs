using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Time;
namespace ConstructionEmpire.Data.Save
{
    public enum SessionCommandKind
    {SubmitBid,StartProject,AssignCrew,ReserveCrew,CancelBooking,RequestQuotes,AwardSubcontractor,ReleaseCrew,OrderMaterial,Refuel,Repair,TrainCrew,DrawLoan,RenameCompany,StartResearch,SetAutomation,OpenOffice,HireCrew,GenerateOpportunities,AcquireEquipment,ReleaseEquipment,SetWorkPolicy,ProposeChangeOrder,NegotiateChangeOrder,DecideChangeOrder,SetInsurance,OrderSupplyPlan}
    [DataContract]
    public sealed class SessionCommandData
    {
        [DataMember]public SessionCommandKind Kind;
        [DataMember]public long Hour;
        [DataMember]public string Id,OtherId,ThirdId,Text;
        [DataMember]public List<string> EquipmentIds=new List<string>();
        [DataMember]public double Amount,Value;
        [DataMember]public long Start,End;
        [DataMember]public bool Flag,OtherFlag;
        [DataMember]public bool Failed;
    }
    [DataContract]
    public sealed class SessionSaveData
    {
        [DataMember] public string PresentationJson;
        public const int CurrentVersion=1;
        public const string CurrentRules="construction-empire-release-1";
        [DataMember(IsRequired=true)]public int Version=CurrentVersion;
        [DataMember(IsRequired=true)]public string Rules=CurrentRules;
        [DataMember(IsRequired=true)]public SessionStartData Initial;
        [DataMember(IsRequired=true)]public List<SessionCommandData> Commands=new List<SessionCommandData>();
        [DataMember(IsRequired=true)]public GameTimeData Clock;
        [DataMember]public string StateProof;
    }
}
