using System;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Subcontractors
{
    public enum SubcontractorSpecialty { Electrical,Plumbing,HVAC,Roofing,Landscaping,Structural }
    [Serializable,DataContract] public sealed class SubcontractorCompanyData : SaveDataBase
    {
        [DataMember] public string Id,Name,Personality;
        [DataMember] public SubcontractorSpecialty Specialty;
        [DataMember] public double QuotePricingTendency=1,Reliability=.9,Quality=.8,Reputation=70,TimelineMultiplier=1;
        [DataMember] public bool IsAvailable=true;
        [DataMember] public int Capacity=1,ActiveAssignments;
        public bool CanAcceptWork=>IsAvailable && ActiveAssignments<Capacity;
        public void Validate()
        {
            ConstructionValidation.Text(Id,nameof(Id));ConstructionValidation.Text(Name,nameof(Name));ConstructionValidation.Positive(QuotePricingTendency,nameof(QuotePricingTendency));ConstructionValidation.Positive(TimelineMultiplier,nameof(TimelineMultiplier));
            ConstructionValidation.Unit(Reliability,nameof(Reliability));ConstructionValidation.Unit(Quality,nameof(Quality));ConstructionValidation.Percent(Reputation,nameof(Reputation));
            if(!Enum.IsDefined(typeof(SubcontractorSpecialty),Specialty) || Capacity<1 || ActiveAssignments<0 || ActiveAssignments>Capacity)throw new ArgumentException("Invalid subcontractor specialty or capacity.");
        }
        public SubcontractorCompanyData Copy()=>(SubcontractorCompanyData)MemberwiseClone();
        public override void Migrate(){if(SchemaVersion!=1){base.Migrate();return;}Validate();SchemaVersion=2;}
    }
    [Serializable] public sealed class SubcontractorSettings
    {
        public double BaseHourlyRate=60,QuotePriceVariation=.1,QuoteTimelineVariation=.1;
        public int DelayHours=24;
        public void Validate(){ConstructionValidation.Positive(BaseHourlyRate,nameof(BaseHourlyRate));ConstructionValidation.Unit(QuotePriceVariation,nameof(QuotePriceVariation));ConstructionValidation.Unit(QuoteTimelineVariation,nameof(QuoteTimelineVariation));if(QuotePriceVariation>=1 || QuoteTimelineVariation>=1 || DelayHours<1)throw new ArgumentException("Invalid subcontractor settings.");}
    }
    public sealed class SubcontractorQuote
    {
        public string Id {get;} public string ProjectId {get;}public string PhaseId {get;}public string CompanyId {get;}public decimal Price {get;}public int DurationHours {get;}public double Reliability {get;}public double Quality {get;}
        public string ReliabilitySignal=>Reliability>=.9 ? "Dependable" : Reliability>=.75 ? "Variable" : "At risk";
        public SubcontractorQuote(string id,string project,string phase,string company,decimal price,int duration,double reliability,double quality){Id=id;ProjectId=project;PhaseId=phase;CompanyId=company;Price=price;DurationHours=duration;Reliability=reliability;Quality=quality;}
    }
    public sealed class SubcontractAward
    {
        public SubcontractorQuote Quote {get;internal set;} public bool Delayed {get;internal set;}public long AvailableFromHour {get;internal set;}public bool Completed {get;internal set;}
        public SubcontractAward(SubcontractorQuote quote,bool delayed,long start){Quote=quote;Delayed=delayed;AvailableFromHour=start;}
        public void SetPerformance(bool delayed,long availableFrom){Delayed=delayed;AvailableFromHour=availableFrom;}
        public void MarkCompleted()=>Completed=true;
    }
    public readonly struct SubcontractorChangedEvent
    {public string ProjectId {get;}public string PhaseId {get;}public SubcontractorChangedEvent(string project,string phase){ProjectId=project;PhaseId=phase;}}
}
