using System.Collections.Generic;
namespace ConstructionEmpire.Data.Construction
{
    public sealed class StarterWorkforce
    {
        public List<EmployeeData> Workers { get; } = new List<EmployeeData>();
        public List<EquipmentRuntimeData> Equipment { get; } = new List<EquipmentRuntimeData>();
    }
    public static class SampleFleetAndCrewDataFactory
    {
        // Provisional authored balance values; generated assets expose these for tuning.
        public static StarterWorkforce CreateStarterWorkersAndEquipment()
        {
            var result=new StarterWorkforce();
            string[] names={"Alex Mercer","Jamie Brooks","Sam Okoro","Robin Vale","Casey Shah","Morgan Ellis"};
            WorkerType[] types={WorkerType.GeneralLaborer,WorkerType.Carpenter,WorkerType.Mason,WorkerType.GeneralLaborer,WorkerType.HeavyEquipmentOperator,WorkerType.SiteSupervisor};
            double[] skills={.6,.75,.7,.55,.8,.85}; double[] years={2,5,4,1,6,10}; double[] wages={18,26,25,18,29,35};
            for(int i=0;i<names.Length;i++) result.Workers.Add(new EmployeeData {Id="employee-"+(i+1),Name=names[i],Age=22+(int)years[i],WorkerType=types[i],Skill=skills[i],Experience=years[i],
                Productivity=1,Reliability=.95,Safety=.9,WagePerHour=wages[i],Morale=.8,Fatigue=0,Specialty=types[i].ToString(),
                Certifications=new List<string> {i==4 ? "Excavator operation" : i==5 ? "Site supervision" : "Site safety"} });
            result.Equipment.Add(new EquipmentRuntimeData {Id="pickup-1",DefinitionId="pickup",Name="Utility Pickup",EquipmentType=EquipmentType.PickupTruck,
                Condition=1,Location="Company Yard",PurchasePrice=24000,RentalPricePerDay=85,MaintenanceCost=150,CurrentFuel=70,FuelCapacity=70,FuelPerHour=5,CostPerHour=12,ProductivityMultiplier=1});
            result.Equipment.Add(new EquipmentRuntimeData {Id="excavator-1",DefinitionId="small-excavator",Name="Small Excavator",EquipmentType=EquipmentType.SmallExcavator,
                Condition=1,Location="Company Yard",PurchasePrice=38000,RentalPricePerDay=160,MaintenanceCost=250,CurrentFuel=50,FuelCapacity=50,FuelPerHour=4,CostPerHour=35,ProductivityMultiplier=1});
            foreach(var worker in result.Workers) worker.Validate(); foreach(var machine in result.Equipment) machine.Validate(); return result;
        }
    }
}
