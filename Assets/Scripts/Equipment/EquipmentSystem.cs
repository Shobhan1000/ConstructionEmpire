using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Equipment
{
    public readonly struct EquipmentPurchasedEvent : IGameEvent
    {
        public readonly string EquipmentId;
        public EquipmentPurchasedEvent(string equipmentId) { EquipmentId = equipmentId; }
    }

    public readonly struct EquipmentBrokenDownEvent : IGameEvent
    {
        public readonly string EquipmentId;
        public EquipmentBrokenDownEvent(string equipmentId) { EquipmentId = equipmentId; }
    }

    /// <summary>Fleet management: purchase/rent, condition, breakdowns (spec sections 12-14).</summary>
    public class EquipmentSystem
    {
        private readonly ContentLibrary _content;
        private readonly CompanySystem _companySystem;
        private readonly SimulationManager _simulationManager;
        private readonly SeededRandom _random;

        public List<EquipmentRuntimeData> Fleet { get; private set; } = new List<EquipmentRuntimeData>();

        public EquipmentSystem(ContentLibrary content, CompanySystem companySystem, SimulationManager simulationManager, SeededRandom random)
        {
            _content = content;
            _companySystem = companySystem;
            _simulationManager = simulationManager;
            _random = random;

            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        public EquipmentRuntimeData PurchaseEquipment(EquipmentType type)
        {
            var def = _content.GetEquipment(type);
            if (def == null) return null;
            if (!_companySystem.TrySpendCash(def.PurchasePrice, "Purchased " + def.DisplayName)) return null;

            var instance = new EquipmentRuntimeData
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = type,
                DisplayName = def.DisplayName,
                Condition = 100f,
                Status = EquipmentStatus.Available,
                IsOwned = true
            };

            Fleet.Add(instance);
            EventBus.Publish(new EquipmentPurchasedEvent(instance.Id));
            return instance;
        }

        public EquipmentRuntimeData RentEquipment(EquipmentType type, int days, DateTime currentDate)
        {
            var def = _content.GetEquipment(type);
            if (def == null) return null;

            double totalCost = def.DailyRentalPrice * days;
            if (!_companySystem.TrySpendCash(totalCost, "Rented " + def.DisplayName + " (" + days + " days)")) return null;

            var instance = new EquipmentRuntimeData
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = type,
                DisplayName = def.DisplayName,
                Condition = 100f,
                Status = EquipmentStatus.Available,
                IsOwned = false,
                RentalReturnDateIso = currentDate.AddDays(days).ToString("o")
            };

            Fleet.Add(instance);

            string equipmentId = instance.Id;
            _simulationManager.ScheduleEvent(currentDate.AddDays(days), () => ReturnRental(equipmentId));

            return instance;
        }

        private void ReturnRental(string equipmentId)
        {
            var instance = Fleet.FirstOrDefault(e => e.Id == equipmentId);
            if (instance == null) return;
            Fleet.Remove(instance);
        }

        public void AssignEquipmentToProject(string equipmentId, string projectId)
        {
            var instance = Fleet.FirstOrDefault(e => e.Id == equipmentId);
            if (instance == null) return;

            instance.CurrentProjectId = projectId;
            instance.Status = EquipmentStatus.Working;
        }

        public void ReleaseEquipment(string equipmentId)
        {
            var instance = Fleet.FirstOrDefault(e => e.Id == equipmentId);
            if (instance == null) return;

            instance.CurrentProjectId = null;
            if (instance.Status != EquipmentStatus.Broken)
            {
                instance.Status = EquipmentStatus.Available;
            }
        }

        /// <summary>0 if broken or missing, otherwise base productivity scaled by current condition.</summary>
        public float GetEfficiency(string equipmentId)
        {
            var instance = Fleet.FirstOrDefault(e => e.Id == equipmentId);
            if (instance == null || instance.Status == EquipmentStatus.Broken) return 0f;

            var def = _content.GetEquipment(instance.Type);
            float baseProductivity = def != null ? def.BaseProductivity : 1f;
            float conditionFactor = Math.Max(0.2f, instance.Condition / 100f);
            return baseProductivity * conditionFactor;
        }

        public List<EquipmentRuntimeData> CaptureState() => Fleet;

        public void RestoreState(List<EquipmentRuntimeData> fleet)
        {
            Fleet = fleet ?? new List<EquipmentRuntimeData>();
        }

        private void OnDayAdvanced(DayAdvancedEvent evt)
        {
            foreach (var instance in Fleet)
            {
                if (instance.Status != EquipmentStatus.Working) continue;

                var def = _content.GetEquipment(instance.Type);
                float degrade = def != null ? def.ConditionDegradePerWorkingDay : 0.5f;
                instance.Condition = Math.Max(0f, instance.Condition - degrade);

                if (def != null)
                {
                    _companySystem.TrySpendCash(def.DailyMaintenanceCost, "Equipment upkeep: " + instance.DisplayName);
                }

                if (instance.Condition < 15f && instance.Status != EquipmentStatus.Broken)
                {
                    float breakdownChance = (15f - instance.Condition) / 100f; // small, grows as condition worsens
                    if (_random.NextBool(breakdownChance))
                    {
                        instance.Status = EquipmentStatus.Broken;
                        EventBus.Publish(new EquipmentBrokenDownEvent(instance.Id));
                    }
                }
            }
        }
    }
}
