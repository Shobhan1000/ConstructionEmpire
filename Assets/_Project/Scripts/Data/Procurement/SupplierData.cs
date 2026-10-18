using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Procurement
{
    [Serializable,DataContract] public sealed class SupplierOffer
    {
        [DataMember] public string MaterialId;
        [DataMember] public double BasePrice,CurrentPrice,Capacity=10000,Reserved,Quality=.8,Volatility=.03;
        [DataMember] public int LeadHours=4;
        public SupplierOffer Copy()=>(SupplierOffer)MemberwiseClone();
    }
    [Serializable,DataContract] public sealed class SupplierData : SaveDataBase
    {
        [DataMember] public string Id,Name,Personality;
        [DataMember] public double Reliability=.9,Relationship=.5;
        [DataMember] public List<SupplierOffer> Offers=new List<SupplierOffer>();
        public void Validate()
        {
            ConstructionValidation.Text(Id,nameof(Id));ConstructionValidation.Text(Name,nameof(Name));ConstructionValidation.Unit(Reliability,nameof(Reliability));ConstructionValidation.Unit(Relationship,nameof(Relationship));
            if(Offers==null || Offers.Count==0 || Offers.Any(x=>x==null) || Offers.Select(x=>x.MaterialId).Distinct().Count()!=Offers.Count) throw new ArgumentException("Unique supplier offers are required.");
            foreach(var o in Offers) { ConstructionValidation.Text(o.MaterialId,"Material");ConstructionValidation.Positive(o.BasePrice,"Base price");ConstructionValidation.Positive(o.CurrentPrice,"Price");ConstructionValidation.Positive(o.Capacity,"Capacity");ConstructionValidation.NonNegative(o.Reserved,"Reserved");ConstructionValidation.Unit(o.Quality,"Quality");ConstructionValidation.Unit(o.Volatility,"Volatility");if(o.LeadHours<2 || o.Reserved>o.Capacity) throw new ArgumentException("Invalid lead time or capacity."); }
        }
        public SupplierData Copy() {var copy=(SupplierData)MemberwiseClone();copy.Offers=Offers.Select(x=>x.Copy()).ToList();return copy;}
        public override void Migrate() {if(SchemaVersion!=1) {base.Migrate();return;}Validate();SchemaVersion=2;}
    }
    [Serializable] public sealed class ProcurementSettings
    {
        public double MinimumPriceFactor=.6,MaximumPriceFactor=1.6,ExpeditedTimeFactor=.5,ExpeditedPremium=.25,BulkThreshold=100,BulkDiscount=.1,RelationshipGain=.01;
        public int DelayHours=24;
        public void Validate()
        {
            foreach(var x in new[]{MinimumPriceFactor,MaximumPriceFactor,ExpeditedTimeFactor,BulkThreshold}) ConstructionValidation.Positive(x,"Procurement setting");
            foreach(var x in new[]{ExpeditedPremium,BulkDiscount,RelationshipGain}) ConstructionValidation.Unit(x,"Procurement fraction");
            if(MinimumPriceFactor>1 || MaximumPriceFactor<1 || ExpeditedTimeFactor>=1 || BulkDiscount>=1 || DelayHours<1) throw new ArgumentException("Invalid procurement settings.");
        }
    }
    public enum ShippingMethod { Standard, Expedited }
    public enum ProcurementStatus { Scheduled, Delayed, Delivered }
    public sealed class ProcurementOrder
    {
        public string Id,ProjectId,MaterialId,SupplierId;public double Quantity,UnitPrice,Discount,Cost;public long DueHour;public ShippingMethod Shipping;public ProcurementStatus Status;
        public bool Delivered=>Status==ProcurementStatus.Delivered;
        public bool ReliabilityChecked;
    }
    public readonly struct ProcurementChangedEvent { public string ProjectId {get;} public ProcurementChangedEvent(string id) {ProjectId=id;} }
}
