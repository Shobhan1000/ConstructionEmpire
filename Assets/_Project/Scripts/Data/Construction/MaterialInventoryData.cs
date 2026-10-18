using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Data.Construction
{
    [Serializable, DataContract]
    public sealed class MaterialQuantity
    {
        [DataMember] public string MaterialId;
        [DataMember] public double Quantity;
        public MaterialQuantity() { }
        public MaterialQuantity(string id,double quantity) { MaterialId=id; Quantity=quantity; }
    }
    [Serializable, DataContract]
    public sealed class MaterialInventoryData : SaveDataBase
    {
        [DataMember] public List<MaterialQuantity> Stock = new List<MaterialQuantity>();
        public void Validate()
        {
            if (Stock==null) throw new ArgumentException("Stock is required.");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in Stock)
            {
                if(item==null) throw new ArgumentException("Null stock item.");
                ConstructionValidation.Text(item.MaterialId,"MaterialId"); ConstructionValidation.NonNegative(item.Quantity,"Quantity");
                if(!ids.Add(item.MaterialId)) throw new ArgumentException("Duplicate material ID.");
            }
        }
        public double GetQuantity(string id) { Validate(); return Stock.Find(x=>x.MaterialId==id)?.Quantity ?? 0; }
        public void Add(string id,double quantity)
        {
            Validate(); ConstructionValidation.Text(id,nameof(id)); ConstructionValidation.NonNegative(quantity,nameof(quantity));
            var item=Stock.Find(x=>x.MaterialId==id);
            double next=(item?.Quantity??0)+quantity; ConstructionValidation.NonNegative(next,"stock total");
            if(item==null) Stock.Add(new MaterialQuantity(id,next)); else item.Quantity=next;
        }
        public bool TryConsume(string id,double quantity)
        {
            Validate(); ConstructionValidation.Text(id,nameof(id)); ConstructionValidation.NonNegative(quantity,nameof(quantity));
            var item=Stock.Find(x=>x.MaterialId==id);
            if(quantity==0) return true;
            if(item==null || item.Quantity < quantity) return false;
            item.Quantity-=quantity; return true;
        }
        public override void Migrate() { if(SchemaVersion!=1) { base.Migrate(); return; } Validate(); SchemaVersion=2; }
    }
}
