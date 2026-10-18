using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Bidding;
namespace ConstructionEmpire.Data.Construction
{
    [Serializable]
    public sealed class ManagementSettings
    {
        public string OfficeName="Small Office", OfficeLocation="Local region";
        public double FuelPricePerLitre=1.65, DailyMaterialVolatility=.03;
        public float CompletionReputationReward=1;
        public int DeadlineWarningHours=48;
        public BidderCapabilities Capabilities=new BidderCapabilities { Experience=.35f, Quality=.6f,ClientRelationship=.3f,Certifications=.6f,MarketFit=.5f };
        public List<MaterialCatalogEntry> Materials=new List<MaterialCatalogEntry>();
        public void Validate()
        {
            ConstructionValidation.NonNegative(FuelPricePerLitre,nameof(FuelPricePerLitre));
            ConstructionValidation.Unit(DailyMaterialVolatility,nameof(DailyMaterialVolatility));
            ConstructionValidation.NonNegative(CompletionReputationReward,nameof(CompletionReputationReward));
            if(DeadlineWarningHours<0) throw new ArgumentOutOfRangeException(nameof(DeadlineWarningHours));
            if(Materials==null || Capabilities==null) throw new ArgumentException("Management configuration is incomplete.");
            Capabilities.Validate(); var ids=new HashSet<string>();
            foreach(var material in Materials) { material.Validate(); if(!ids.Add(material.Id)) throw new ArgumentException("Duplicate material."); }
        }
        public static ManagementSettings CreateStarter()
        {
            var settings=new ManagementSettings();
            string[] ids={"concrete","cement","sand","gravel","steel","timber","bricks","glass","drywall","insulation","roofing","pipe","cable","fixtures","paint","flooring","site-supplies"};
            string[] names={"Concrete","Cement","Sand","Gravel","Steel","Lumber","Bricks","Glass","Drywall","Insulation","Roofing","Pipes","Electrical cable","Fixtures","Paint","Flooring","Site supplies"};
            string[] units={"m³","bag","tonne","tonne","tonne","m","unit","m²","sheet","m²","m²","m","m","unit","litre","m²","kit"};
            double[] prices={120,8,45,40,850,12,1.2,60,18,10,24,8,3,75,12,30,25};
            for(int i=0;i<ids.Length;i++) settings.Materials.Add(new MaterialCatalogEntry { Id=ids[i],Name=names[i],Unit=units[i],Price=prices[i],
                Supplier="Northbridge Building Supply",LeadHours=4,Availability=10000,Quality=.8,StorageRequirement="Covered company storage" });
            return settings;
        }
    }
}
