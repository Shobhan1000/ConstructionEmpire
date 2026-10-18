using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Finance
{
    public enum PaymentStage { Upfront, Progress, Final, Retention }
    [Serializable,DataContract]
    public sealed class ContractPayment
    {
        [DataMember] public string Id;
        [DataMember] public PaymentStage Stage;
        [DataMember] public decimal Fraction;
        [DataMember] public double ProgressThreshold;
        [DataMember] public int DelayHours;
        [DataMember] public bool Invoiced;
        public ContractPayment Copy()=>(ContractPayment)MemberwiseClone();
    }
    [Serializable,DataContract]
    public sealed class ContractData : SaveDataBase
    {
        [DataMember] public string Id,ProjectId,ClientId,ChangeOrderRulesReference;
        [DataMember] public decimal ContractValue,RetentionPercentage,PenaltyPerLateDay,MaximumPenaltyFraction;
        [DataMember] public long StartHour,CompletionDeadlineHour,CompletionHour=-1,WarrantyDurationHours;
        [DataMember] public string RetentionReleaseCondition="Warranty period completed";
        [DataMember] public double RequiredQuality=.6,QualityOutcome=.6,Progress;
        [DataMember] public bool Started,Completed,RetentionInvoiced,ReputationApplied;
        [DataMember] public decimal RevenueRecognized;
        [DataMember] public List<ContractPayment> PaymentSchedule=new List<ContractPayment>();
        public decimal RetentionAmount=>decimal.Round(ContractValue*RetentionPercentage,2);
        public long RetentionReleaseHour=>CompletionHour<0 ? long.MaxValue : checked(CompletionHour+WarrantyDurationHours);
        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(ProjectId) || string.IsNullOrWhiteSpace(ClientId)) throw new ArgumentException("Contract identity is required.");
            if(ContractValue<=0 || RetentionPercentage<0 || RetentionPercentage>=1 || PenaltyPerLateDay<0 || MaximumPenaltyFraction<0 || MaximumPenaltyFraction>1) throw new ArgumentException("Invalid contract amounts.");
            if(StartHour<0 || CompletionDeadlineHour<StartHour || WarrantyDurationHours<0) throw new ArgumentException("Invalid contract dates.");
            if(double.IsNaN(RequiredQuality) || RequiredQuality<0 || RequiredQuality>1 || double.IsNaN(QualityOutcome) || QualityOutcome<0 || QualityOutcome>1) throw new ArgumentException("Quality must be 0..1.");
            if(PaymentSchedule==null || PaymentSchedule.Count==0 || PaymentSchedule.Select(x=>x.Id).Distinct().Count()!=PaymentSchedule.Count) throw new ArgumentException("Unique payment milestones are required.");
            foreach(var payment in PaymentSchedule)
                if(string.IsNullOrWhiteSpace(payment.Id) || payment.Stage==PaymentStage.Retention || !Enum.IsDefined(typeof(PaymentStage),payment.Stage) || payment.Fraction<=0 || payment.DelayHours<0 || double.IsNaN(payment.ProgressThreshold) || payment.ProgressThreshold<0 || payment.ProgressThreshold>100) throw new ArgumentException("Invalid payment milestone. Retention is calculated separately.");
            if(PaymentSchedule.Sum(x=>x.Fraction)!=1m) throw new ArgumentException("Non-retention payment fractions must sum to one.");
        }
        public ContractData Copy() { var copy=(ContractData)MemberwiseClone(); copy.PaymentSchedule=PaymentSchedule.Select(x=>x.Copy()).ToList(); return copy; }
        public override void Migrate() { if(SchemaVersion!=1) { base.Migrate(); return; } Validate(); SchemaVersion=2; }
    }
}
