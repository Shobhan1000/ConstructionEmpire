using QualityLevel = ConstructionEmpire.Data.QualityLevel;
using UnityEngine;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Content
{
    /// <summary>
    /// Builds a full default content set in memory via
    /// ScriptableObject.CreateInstance, so the game has enough data to run
    /// a complete bid-to-completion loop without anyone hand-authoring
    /// asset files first. Treat this as seed/placeholder content - swap
    /// individual definitions for hand-tuned .asset files later without
    /// changing any system code, since everything reads through
    /// ContentLibrary rather than referencing these directly.
    /// </summary>
    public static class DefaultContentFactory
    {
        public static ContentLibrary BuildDefaultLibrary()
        {
            var library = new ContentLibrary();

            foreach (var material in BuildMaterials()) library.RegisterMaterial(material);
            foreach (var equipment in BuildEquipment()) library.RegisterEquipment(equipment);
            foreach (var workerType in BuildWorkerTypes()) library.RegisterWorkerType(workerType);
            foreach (var phase in BuildPhases()) library.RegisterPhase(phase);
            foreach (var template in BuildProjectTemplates()) library.RegisterProjectTemplate(template);
            foreach (var subcontractor in BuildSubcontractors()) library.RegisterSubcontractor(subcontractor);

            return library;
        }

        private static MaterialDefinition Material(MaterialType type, string name, string unit, double price, int leadTimeDays)
        {
            var m = ScriptableObject.CreateInstance<MaterialDefinition>();
            m.Type = type;
            m.DisplayName = name;
            m.Unit = unit;
            m.BasePricePerUnit = price;
            m.DefaultLeadTimeDays = leadTimeDays;
            return m;
        }

        private static MaterialDefinition[] BuildMaterials()
        {
            return new[]
            {
                Material(MaterialType.Concrete, "Concrete", "cubic yard", 130, 2),
                Material(MaterialType.Cement, "Cement", "bag", 12, 2),
                Material(MaterialType.Sand, "Sand", "ton", 25, 1),
                Material(MaterialType.Gravel, "Gravel", "ton", 22, 1),
                Material(MaterialType.Steel, "Steel", "ton", 850, 5),
                Material(MaterialType.Lumber, "Lumber", "board ft", 4.5, 3),
                Material(MaterialType.Bricks, "Bricks", "unit", 0.6, 3),
                Material(MaterialType.Glass, "Glass", "sq ft", 14, 4),
                Material(MaterialType.Drywall, "Drywall", "sheet", 13, 2),
                Material(MaterialType.Insulation, "Insulation", "sq ft", 1.2, 2),
                Material(MaterialType.Roofing, "Roofing", "sq ft", 5.5, 4),
                Material(MaterialType.Pipes, "Pipes", "linear ft", 3.2, 3),
                Material(MaterialType.ElectricalCable, "Electrical Cable", "linear ft", 1.8, 2),
                Material(MaterialType.Fixtures, "Fixtures", "unit", 90, 5),
                Material(MaterialType.Paint, "Paint", "gallon", 35, 1),
                Material(MaterialType.Flooring, "Flooring", "sq ft", 6.5, 3)
            };
        }

        private static EquipmentDefinition Equipment(EquipmentType type, string name, double purchase, double dailyRental, float productivity, float fuelPerDay, double maintenancePerDay)
        {
            var e = ScriptableObject.CreateInstance<EquipmentDefinition>();
            e.Type = type;
            e.DisplayName = name;
            e.PurchasePrice = purchase;
            e.DailyRentalPrice = dailyRental;
            e.BaseProductivity = productivity;
            e.FuelConsumptionPerDay = fuelPerDay;
            e.DailyMaintenanceCost = maintenancePerDay;
            e.ConditionDegradePerWorkingDay = 0.5f;
            return e;
        }

        private static EquipmentDefinition[] BuildEquipment()
        {
            return new[]
            {
                Equipment(EquipmentType.Excavator, "Excavator", 120000, 450, 1.1f, 25, 40),
                Equipment(EquipmentType.Bulldozer, "Bulldozer", 150000, 500, 1.1f, 30, 45),
                Equipment(EquipmentType.WheelLoader, "Wheel Loader", 90000, 350, 1f, 20, 30),
                Equipment(EquipmentType.Crane, "Crane", 300000, 900, 1.2f, 15, 70),
                Equipment(EquipmentType.ConcreteMixer, "Concrete Mixer", 40000, 180, 1f, 8, 15),
                Equipment(EquipmentType.DumpTruck, "Dump Truck", 80000, 300, 1f, 22, 25),
                Equipment(EquipmentType.Forklift, "Forklift", 35000, 150, 1f, 10, 12),
                Equipment(EquipmentType.ScissorLift, "Scissor Lift", 25000, 120, 1f, 5, 10),
                Equipment(EquipmentType.Telehandler, "Telehandler", 70000, 280, 1.05f, 15, 20),
                Equipment(EquipmentType.Compactor, "Compactor", 15000, 90, 1f, 6, 8),
                Equipment(EquipmentType.Generator, "Generator", 10000, 60, 1f, 12, 5)
            };
        }

        private static WorkerTypeDefinition WorkerTypeDef(WorkerType type, string name, double wage, float productivity)
        {
            var w = ScriptableObject.CreateInstance<WorkerTypeDefinition>();
            w.Type = type;
            w.DisplayName = name;
            w.BaseDailyWage = wage;
            w.BaseProductivity = productivity;
            return w;
        }

        private static WorkerTypeDefinition[] BuildWorkerTypes()
        {
            return new[]
            {
                WorkerTypeDef(WorkerType.GeneralLaborer, "General Laborer", 180, 1f),
                WorkerTypeDef(WorkerType.Carpenter, "Carpenter", 260, 1.1f),
                WorkerTypeDef(WorkerType.Mason, "Mason", 260, 1.1f),
                WorkerTypeDef(WorkerType.Electrician, "Electrician", 320, 1.15f),
                WorkerTypeDef(WorkerType.Plumber, "Plumber", 320, 1.15f),
                WorkerTypeDef(WorkerType.EquipmentOperator, "Equipment Operator", 280, 1.1f),
                WorkerTypeDef(WorkerType.Welder, "Welder", 300, 1.1f),
                WorkerTypeDef(WorkerType.Painter, "Painter", 200, 1f),
                WorkerTypeDef(WorkerType.SiteSupervisor, "Site Supervisor", 380, 1.25f),
                WorkerTypeDef(WorkerType.ProjectManager, "Project Manager", 450, 1.25f)
            };
        }

        private static ConstructionPhaseDefinition Phase(
            string id, string name, int durationDays, double laborCost, float qualityWeight,
            WorkerType[] workers, MaterialRequirement[] materials, EquipmentRequirement[] equipment, string[] prerequisites,
            bool isOutdoorWork, SubcontractorSpecialty relevantSpecialty, bool requiresInspection)
        {
            var p = ScriptableObject.CreateInstance<ConstructionPhaseDefinition>();
            p.PhaseId = id;
            p.DisplayName = name;
            p.BaseDurationDays = durationDays;
            p.BaseLaborCost = laborCost;
            p.QualityImpactWeight = qualityWeight;
            p.RequiredWorkerTypes = workers ?? new WorkerType[0];
            p.RequiredMaterials = materials ?? new MaterialRequirement[0];
            p.RequiredEquipment = equipment ?? new EquipmentRequirement[0];
            p.PrerequisitePhaseIds = prerequisites ?? new string[0];
            p.IsOutdoorWork = isOutdoorWork;
            p.RelevantSpecialty = relevantSpecialty;
            p.RequiresInspection = requiresInspection;
            return p;
        }

        private static MaterialRequirement Mat(MaterialType type, float qty) => new MaterialRequirement { Type = type, Quantity = qty };
        private static EquipmentRequirement Equip(EquipmentType type, int days) => new EquipmentRequirement { Type = type, DaysNeeded = days };

        private static ConstructionPhaseDefinition[] BuildPhases()
        {
            return new[]
            {
                Phase("site_prep", "Site Preparation", 4, 6000, 0.05f,
                    new[] { WorkerType.GeneralLaborer, WorkerType.EquipmentOperator },
                    new MaterialRequirement[0],
                    new[] { Equip(EquipmentType.Excavator, 3), Equip(EquipmentType.Bulldozer, 2) },
                    new string[0],
                    isOutdoorWork: true, relevantSpecialty: SubcontractorSpecialty.None, requiresInspection: false),

                // Foundation inspections are a classic real-world checkpoint
                // (spec section 22's own example list) - fail it and you're
                // redoing rebar/formwork before framing can even start.
                Phase("foundation", "Foundation", 6, 12000, 0.15f,
                    new[] { WorkerType.Mason, WorkerType.GeneralLaborer, WorkerType.EquipmentOperator },
                    new[] { Mat(MaterialType.Concrete, 80), Mat(MaterialType.Steel, 10) },
                    new[] { Equip(EquipmentType.ConcreteMixer, 3) },
                    new[] { "site_prep" },
                    isOutdoorWork: true, relevantSpecialty: SubcontractorSpecialty.None, requiresInspection: true),

                Phase("framing", "Framing", 8, 18000, 0.15f,
                    new[] { WorkerType.Carpenter, WorkerType.GeneralLaborer },
                    new[] { Mat(MaterialType.Lumber, 200), Mat(MaterialType.Steel, 15) },
                    new[] { Equip(EquipmentType.Crane, 2) },
                    new[] { "foundation" },
                    isOutdoorWork: true, relevantSpecialty: SubcontractorSpecialty.StructuralSteel, requiresInspection: false),

                Phase("roofing", "Roofing", 5, 9000, 0.1f,
                    new[] { WorkerType.Carpenter, WorkerType.GeneralLaborer },
                    new[] { Mat(MaterialType.Roofing, 100) },
                    new[] { Equip(EquipmentType.ScissorLift, 2) },
                    new[] { "framing" },
                    isOutdoorWork: true, relevantSpecialty: SubcontractorSpecialty.Roofing, requiresInspection: true),

                Phase("plumbing_rough", "Plumbing (Rough-in)", 4, 7000, 0.1f,
                    new[] { WorkerType.Plumber },
                    new[] { Mat(MaterialType.Pipes, 60) },
                    new EquipmentRequirement[0],
                    new[] { "framing" },
                    isOutdoorWork: false, relevantSpecialty: SubcontractorSpecialty.Plumbing, requiresInspection: true),

                Phase("electrical_rough", "Electrical (Rough-in)", 4, 7500, 0.1f,
                    new[] { WorkerType.Electrician },
                    new[] { Mat(MaterialType.ElectricalCable, 80) },
                    new EquipmentRequirement[0],
                    new[] { "framing" },
                    isOutdoorWork: false, relevantSpecialty: SubcontractorSpecialty.Electrical, requiresInspection: true),

                Phase("insulation", "Insulation", 3, 4000, 0.05f,
                    new[] { WorkerType.GeneralLaborer },
                    new[] { Mat(MaterialType.Insulation, 120) },
                    new EquipmentRequirement[0],
                    new[] { "plumbing_rough", "electrical_rough" },
                    isOutdoorWork: false, relevantSpecialty: SubcontractorSpecialty.None, requiresInspection: false),

                Phase("drywall", "Drywall", 5, 8000, 0.1f,
                    new[] { WorkerType.GeneralLaborer, WorkerType.Painter },
                    new[] { Mat(MaterialType.Drywall, 150) },
                    new EquipmentRequirement[0],
                    new[] { "insulation" },
                    isOutdoorWork: false, relevantSpecialty: SubcontractorSpecialty.None, requiresInspection: false),

                Phase("flooring", "Flooring", 4, 6000, 0.1f,
                    new[] { WorkerType.GeneralLaborer },
                    new[] { Mat(MaterialType.Flooring, 100) },
                    new EquipmentRequirement[0],
                    new[] { "drywall" },
                    isOutdoorWork: false, relevantSpecialty: SubcontractorSpecialty.None, requiresInspection: false),

                Phase("interior_finish", "Interior Finishing", 6, 9000, 0.15f,
                    new[] { WorkerType.Painter, WorkerType.GeneralLaborer },
                    new[] { Mat(MaterialType.Paint, 40), Mat(MaterialType.Fixtures, 20) },
                    new EquipmentRequirement[0],
                    new[] { "flooring" },
                    isOutdoorWork: false, relevantSpecialty: SubcontractorSpecialty.None, requiresInspection: false),

                // Final inspection needs the roof done too, even though
                // roofing doesn't block any interior work directly - keeps
                // the phase graph realistic instead of leaving roofing a
                // dead-end branch with implausibly huge slack (see
                // CriticalPathCalculator). It's also itself an inspection
                // checkpoint, same as the real-world "final inspection".
                Phase("final_inspection", "Final Inspection", 1, 1000, 0.05f,
                    new[] { WorkerType.SiteSupervisor },
                    new MaterialRequirement[0],
                    new EquipmentRequirement[0],
                    new[] { "interior_finish", "roofing" },
                    isOutdoorWork: false, relevantSpecialty: SubcontractorSpecialty.None, requiresInspection: true)
            };
        }

        private static ProjectTemplate Template(ProjectType type, string name, string[] phaseIds, double minValue, double maxValue, QualityLevel quality, float siteDifficulty, float overhead)
        {
            var t = ScriptableObject.CreateInstance<ProjectTemplate>();
            t.Type = type;
            t.DisplayName = name;
            t.PhaseIds = phaseIds;
            t.MinContractValue = minValue;
            t.MaxContractValue = maxValue;
            t.RequiredQuality = quality;
            t.BaseSiteDifficulty = siteDifficulty;
            t.OverheadFraction = overhead;
            return t;
        }

        private static ProjectTemplate[] BuildProjectTemplates()
        {
            return new[]
            {
                Template(ProjectType.HouseRenovation, "House Renovation",
                    new[] { "site_prep", "foundation", "framing", "roofing", "plumbing_rough", "electrical_rough", "insulation", "drywall", "flooring", "interior_finish", "final_inspection" },
                    80000, 180000, QualityLevel.Average, 0.25f, 0.12f),

                Template(ProjectType.SmallWarehouse, "Small Warehouse",
                    new[] { "site_prep", "foundation", "framing", "roofing", "electrical_rough", "interior_finish", "final_inspection" },
                    150000, 400000, QualityLevel.Average, 0.3f, 0.1f),

                Template(ProjectType.RestaurantFitOut, "Restaurant Fit-Out",
                    new[] { "plumbing_rough", "electrical_rough", "insulation", "drywall", "flooring", "interior_finish", "final_inspection" },
                    60000, 140000, QualityLevel.Good, 0.2f, 0.13f),

                Template(ProjectType.OfficeRenovation, "Office Renovation",
                    new[] { "electrical_rough", "insulation", "drywall", "flooring", "interior_finish", "final_inspection" },
                    40000, 110000, QualityLevel.Average, 0.15f, 0.12f),

                Template(ProjectType.ResidentialExtension, "Residential Extension",
                    new[] { "site_prep", "foundation", "framing", "roofing", "electrical_rough", "plumbing_rough", "insulation", "drywall", "interior_finish", "final_inspection" },
                    70000, 160000, QualityLevel.Average, 0.25f, 0.12f)
            };
        }

        private static SubcontractorDefinition Subcontractor(string id, string name, SubcontractorSpecialty specialty, float costMultiplier, float reliability, float quality, float speedFactor)
        {
            var s = ScriptableObject.CreateInstance<SubcontractorDefinition>();
            s.Id = id;
            s.CompanyName = name;
            s.Specialty = specialty;
            s.BaseCostMultiplier = costMultiplier;
            s.BaseReliability = reliability;
            s.BaseQuality = quality;
            s.SpeedFactor = speedFactor;
            return s;
        }

        private static SubcontractorDefinition[] BuildSubcontractors()
        {
            return new[]
            {
                // Electrical - a reliable premium option vs. a cheap gamble.
                Subcontractor("sub_electrical_bright_spark", "Bright Spark Electrical", SubcontractorSpecialty.Electrical, 1.15f, 0.88f, 0.82f, 0.95f),
                Subcontractor("sub_electrical_volt_wire", "Volt & Wire Co.", SubcontractorSpecialty.Electrical, 0.92f, 0.6f, 0.55f, 1.05f),

                // Plumbing - same trade-off shape.
                Subcontractor("sub_plumbing_clearflow", "Clearflow Plumbing", SubcontractorSpecialty.Plumbing, 1.15f, 0.9f, 0.85f, 0.9f),
                Subcontractor("sub_plumbing_budget_pipe", "Budget Pipe Solutions", SubcontractorSpecialty.Plumbing, 0.9f, 0.58f, 0.52f, 1.1f),

                // Roofing.
                Subcontractor("sub_roofing_summit", "Summit Roofing Co.", SubcontractorSpecialty.Roofing, 1.1f, 0.88f, 0.82f, 0.92f),
                Subcontractor("sub_roofing_quickcap", "QuickCap Roofing", SubcontractorSpecialty.Roofing, 0.85f, 0.6f, 0.55f, 1.08f),

                // Structural steel (used by framing).
                Subcontractor("sub_structural_ironframe", "Ironframe Structural", SubcontractorSpecialty.StructuralSteel, 1.2f, 0.9f, 0.85f, 0.9f)
            };
        }
    }
}
