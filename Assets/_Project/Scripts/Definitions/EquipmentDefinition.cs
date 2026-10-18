using ConstructionEmpire.Data.Construction;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/Equipment Definition")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        public string DefinitionId;
        public string DisplayName;
        public EquipmentType EquipmentType;
        public double PurchasePrice, RentalPricePerDay, MaintenanceCost;
        public double FuelCapacity;
        public double FuelPerHour;
        public double CostPerHour;
        public double ProductivityMultiplier=1;
        public EquipmentRuntimeData CreateRuntimeData(string id,string location,double condition,double fuel)
        {
            var data=new EquipmentRuntimeData {Id=id,DefinitionId=DefinitionId,Name=DisplayName,EquipmentType=EquipmentType,Location=location,
                PurchasePrice=PurchasePrice,RentalPricePerDay=RentalPricePerDay,MaintenanceCost=MaintenanceCost,Condition=condition,CurrentFuel=fuel,FuelCapacity=FuelCapacity,FuelPerHour=FuelPerHour,CostPerHour=CostPerHour,ProductivityMultiplier=ProductivityMultiplier};
            data.Validate(); return data;
        }
    }
}
