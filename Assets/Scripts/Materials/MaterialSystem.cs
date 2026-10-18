using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Materials
{
    public readonly struct MaterialOrderedEvent : IGameEvent
    {
        public readonly MaterialType Type;
        public readonly double Quantity;
        public MaterialOrderedEvent(MaterialType type, double quantity) { Type = type; Quantity = quantity; }
    }

    public readonly struct MaterialDeliveredEvent : IGameEvent
    {
        public readonly MaterialType Type;
        public readonly double Quantity;
        public MaterialDeliveredEvent(MaterialType type, double quantity) { Type = type; Quantity = quantity; }
    }

    /// <summary>
    /// Company-wide material inventory and procurement (spec sections
    /// 15-16). Simplification: one shared warehouse rather than
    /// per-project stockpiles - good enough while a company runs a
    /// handful of projects; revisit if that stops being true.
    /// </summary>
    public class MaterialSystem
    {
        private readonly ContentLibrary _content;
        private readonly CompanySystem _companySystem;
        private readonly SimulationManager _simulationManager;

        private Dictionary<MaterialType, MaterialStockEntry> _inventory = new Dictionary<MaterialType, MaterialStockEntry>();

        public MaterialSystem(ContentLibrary content, CompanySystem companySystem, SimulationManager simulationManager)
        {
            _content = content;
            _companySystem = companySystem;
            _simulationManager = simulationManager;
        }

        public double GetOnHand(MaterialType type) => _inventory.TryGetValue(type, out var entry) ? entry.QuantityOnHand : 0;
        public double GetOnOrder(MaterialType type) => _inventory.TryGetValue(type, out var entry) ? entry.QuantityOnOrder : 0;

        /// <summary>On-hand plus already-on-order, so callers don't double-order what's already coming.</summary>
        public double GetAvailable(MaterialType type) => GetOnHand(type) + GetOnOrder(type);

        public bool OrderMaterial(MaterialType type, double quantity, string reason, DateTime currentDate)
        {
            if (quantity <= 0) return false;

            var def = _content.GetMaterial(type);
            if (def == null) return false;

            double cost = quantity * def.BasePricePerUnit;
            if (!_companySystem.TrySpendCash(cost, "Materials: " + reason)) return false;

            var entry = GetOrCreateEntry(type);
            entry.QuantityOnOrder += quantity;

            var deliveryDate = currentDate.AddDays(def.DefaultLeadTimeDays);
            _simulationManager.ScheduleEvent(deliveryDate, () => DeliverMaterial(type, quantity));

            EventBus.Publish(new MaterialOrderedEvent(type, quantity));
            return true;
        }

        private void DeliverMaterial(MaterialType type, double quantity)
        {
            var entry = GetOrCreateEntry(type);
            entry.QuantityOnOrder = Math.Max(0, entry.QuantityOnOrder - quantity);
            entry.QuantityOnHand += quantity;

            EventBus.Publish(new MaterialDeliveredEvent(type, quantity));
        }

        public bool ConsumeMaterial(MaterialType type, double quantity)
        {
            if (quantity <= 0) return true;

            var entry = GetOrCreateEntry(type);
            if (entry.QuantityOnHand < quantity) return false;

            entry.QuantityOnHand -= quantity;
            return true;
        }

        private MaterialStockEntry GetOrCreateEntry(MaterialType type)
        {
            if (!_inventory.TryGetValue(type, out var entry))
            {
                entry = new MaterialStockEntry { Type = type };
                _inventory[type] = entry;
            }

            return entry;
        }

        public List<MaterialStockEntry> CaptureState() => _inventory.Values.ToList();

        public void RestoreState(List<MaterialStockEntry> state)
        {
            _inventory = new Dictionary<MaterialType, MaterialStockEntry>();
            if (state == null) return;

            foreach (var entry in state)
            {
                _inventory[entry.Type] = entry;
            }
        }
    }
}
