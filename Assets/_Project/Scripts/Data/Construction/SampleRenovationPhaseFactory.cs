using System.Collections.Generic;
namespace ConstructionEmpire.Data.Construction
{
    public static class SampleRenovationPhaseFactory
    {
        // Authored starter balance, copied to editable phase assets by the generator.
        public static List<ConstructionPhaseRuntimeData> CreateHouseRenovationPhases()
        {
            var phases = new List<ConstructionPhaseRuntimeData> {
                Phase("site-preparation","Site Preparation",4,24,100,WorkerType.GeneralLaborer,null,"site-supplies",4),
                Phase("excavation","Excavation",8,48,300,WorkerType.HeavyEquipmentOperator,"site-preparation",null,0),
                Phase("foundation","Foundation",12,72,800,WorkerType.Mason,"excavation","concrete",12),
                Phase("framing","Framing",16,96,600,WorkerType.Carpenter,"foundation","timber",80),
                Phase("roofing","Roofing",8,48,400,WorkerType.Carpenter,"framing","roofing",40),
                Phase("exterior-walls","Exterior Walls",8,48,400,WorkerType.Mason,"framing","bricks",80),
                Phase("plumbing","Plumbing",8,48,300,WorkerType.Plumber,"exterior-walls","pipe",40),
                Phase("electrical","Electrical",8,48,300,WorkerType.Electrician,"exterior-walls","cable",100),
                Phase("hvac","HVAC",6,36,300,WorkerType.GeneralLaborer,"roofing","fixtures",4),
                Phase("insulation","Insulation",6,36,200,WorkerType.GeneralLaborer,"hvac","insulation",40),
                Phase("drywall","Drywall",8,48,250,WorkerType.Carpenter,"insulation","drywall",40),
                Phase("flooring","Flooring",6,36,200,WorkerType.Carpenter,"drywall","flooring",40),
                Phase("finishes","Interior Finishing",12,72,500,WorkerType.Painter,"flooring","paint",30),
                Phase("landscaping","Landscaping",4,24,150,WorkerType.GeneralLaborer,"exterior-walls","gravel",6),
                Phase("inspection","Inspection",2,12,100,WorkerType.SiteSupervisor,"finishes",null,0),
                Phase("completion","Completion",1,6,50,WorkerType.ProjectManager,"inspection",null,0)
            };
            phases[1].EquipmentRequirements.Add(new EquipmentRequirement(EquipmentType.SmallExcavator,1));
            phases[9].PrerequisiteIds.AddRange(new[]{"electrical","plumbing"});
            phases[14].PrerequisiteIds.Add("landscaping");
            return phases;
        }
        private static ConstructionPhaseRuntimeData Phase(string id,string name,double duration,double labor,double cost,WorkerType trade,string prerequisite,string material,double quantity)
        {
            var phase=new ConstructionPhaseRuntimeData {Id=id,Name=name,DurationHours=duration,LaborHours=labor,Cost=cost,QualityImpact=1,Risk=.05,
                CompatibleTrades=new List<WorkerType> {trade}};
            if(prerequisite!=null) phase.PrerequisiteIds.Add(prerequisite);
            if(material!=null) phase.MaterialRequirements.Add(new MaterialQuantity(material,quantity)); return phase;
        }
    }
}
