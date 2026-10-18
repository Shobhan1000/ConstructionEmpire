using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Procurement;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Procurement
{
    public sealed class SupplierSystem : IRegisterableSystem
    {
        private readonly List<SupplierData> suppliers;private readonly SeededRandom random;private readonly ProcurementSettings settings;private long lastHour;
        private double marketIndex=1;
        public void SetMarketIndex(double value)
        {ConstructionEmpire.Data.Construction.ConstructionValidation.Positive(value,"Market price index");double ratio=value/marketIndex;foreach(var s in suppliers)foreach(var offer in s.Offers){offer.BasePrice*=ratio;offer.CurrentPrice*=ratio;}marketIndex=value;GameEvents.Publish(new MaterialPricesChangedEvent());}
        public IReadOnlyList<SupplierData> Suppliers=>suppliers.AsReadOnly();
        public SupplierSystem(IEnumerable<SupplierData> suppliers,SeededRandom random,ProcurementSettings settings)
        {
            this.suppliers=suppliers.Select(x=>x.Copy()).OrderBy(x=>x.Id,StringComparer.Ordinal).ToList();this.random=random??throw new ArgumentNullException(nameof(random));this.settings=settings;settings.Validate();
            foreach(var s in this.suppliers)s.Validate();if(this.suppliers.Select(x=>x.Id).Distinct().Count()!=this.suppliers.Count)throw new ArgumentException("Duplicate supplier.");
        }
        public IEnumerable<SupplierData> ForMaterial(string material)=>suppliers.Where(x=>x.Offers.Any(o=>o.MaterialId==material));
        public SupplierData Get(string id)=>suppliers.Single(x=>x.Id==id);
        public SupplierOffer Offer(string supplier,string material)=>Get(supplier).Offers.Single(x=>x.MaterialId==material);
        public void OnTick(TickContext tick)
        {
            if(tick.TotalHours<=lastHour)return;lastHour=tick.TotalHours;if(!tick.StartsDay)return;
            foreach(var s in suppliers)foreach(var offer in s.Offers.OrderBy(x=>x.MaterialId,StringComparer.Ordinal))
                offer.CurrentPrice=Math.Max(offer.BasePrice*settings.MinimumPriceFactor,Math.Min(offer.BasePrice*settings.MaximumPriceFactor,offer.CurrentPrice*(1+(random.NextDouble()*2-1)*offer.Volatility)));
            GameEvents.Publish(new MaterialPricesChangedEvent());
        }
    }
}
