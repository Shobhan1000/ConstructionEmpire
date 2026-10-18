using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Data.Expansion
{
    public enum RegionUnlockRule { ReputationOrCapital, ReputationAndCapital }
    [Serializable,DataContract]
    public sealed class WorldMapData : SaveDataBase
    {
        [DataMember]public string Id,Name;
        [DataMember(EmitDefaultValue=false)]public string CountryId,CountryName,MarketDescription;
        [DataMember(EmitDefaultValue=false)]public double OperatingCostMultiplier;
        [DataMember(EmitDefaultValue=false)]public List<string> PreferredProjectTypes;
        [DataMember(EmitDefaultValue=false)]public int PreferredCategoryWeight;
        public double CostFactor=>OperatingCostMultiplier>0?OperatingCostMultiplier:1;
        [DataMember]public double X,Y,ReputationThreshold,CapitalThreshold,PriceMultiplier=1;
        [DataMember]public bool StartingRegion;
        [DataMember]public RegionUnlockRule UnlockRule;
        [DataMember]public List<string> ProjectTypes=new List<string>(),CompetitorIds=new List<string>(),SupplierNames=new List<string>(),DepotNames=new List<string>();
        public bool MeetsThreshold(double reputation,double cash)=>StartingRegion || (UnlockRule==RegionUnlockRule.ReputationAndCapital ? reputation>=ReputationThreshold && cash>=CapitalThreshold : reputation>=ReputationThreshold || cash>=CapitalThreshold);
        public WorldMapData Copy(){var c=(WorldMapData)MemberwiseClone();c.ProjectTypes=new List<string>(ProjectTypes);c.PreferredProjectTypes=PreferredProjectTypes==null?null:new List<string>(PreferredProjectTypes);c.CompetitorIds=new List<string>(CompetitorIds);c.SupplierNames=new List<string>(SupplierNames);c.DepotNames=new List<string>(DepotNames);return c;}
        public double DistanceTo(double x,double y)=>Math.Sqrt((X-x)*(X-x)+(Y-y)*(Y-y));
        public void Validate(){ConstructionValidation.NonNegative(OperatingCostMultiplier,"Regional operating cost");if(PreferredCategoryWeight<0 || PreferredCategoryWeight>10 || PreferredProjectTypes!=null && PreferredProjectTypes.Any(t=>!ProjectTypes.Contains(t)))throw new ArgumentException("Invalid regional opportunity weighting.");ConstructionValidation.Text(Id,"Region ID");ConstructionValidation.Text(Name,"Region name");ConstructionValidation.NonNegative(X,"X");ConstructionValidation.NonNegative(Y,"Y");ConstructionValidation.Percent(ReputationThreshold,"Reputation gate");ConstructionValidation.NonNegative(CapitalThreshold,"Capital gate");ConstructionValidation.Positive(PriceMultiplier,"Price multiplier");if(!Enum.IsDefined(typeof(RegionUnlockRule),UnlockRule) || ProjectTypes==null || ProjectTypes.Count==0 || CompetitorIds==null || CompetitorIds.Count==0 || ProjectTypes.Any(string.IsNullOrWhiteSpace) || CompetitorIds.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Region requires project types and competitors.");}
        public override void Migrate(){if(SchemaVersion!=1){base.Migrate();return;}Validate();SchemaVersion=2;}
    }
    public static class SampleExpansionDataFactory
    {
        public static List<WorldMapData> CreateStarterRegions()=>new List<WorldMapData> {
            Region("westmere","Westmere",30,40,0,0,1,true,new[]{"Residential","Warehouse","Hospitality","Office"},new[]{"competitor-1","competitor-2","competitor-3","competitor-4","competitor-5"}),
            Region("riverside","Riverside",150,90,55,200000,1.05,false,new[]{"Residential","Hospitality"},new[]{"competitor-1","competitor-4","competitor-5"}),
            Region("northport","Northport",270,30,65,350000,1.2,false,new[]{"Warehouse","Office"},new[]{"competitor-2","competitor-3","competitor-4"}),
            Region("hillcrest","Hillcrest",210,210,75,500000,1.3,false,new[]{"Residential","Office"},new[]{"competitor-2","competitor-4","competitor-5"})};
        private static WorldMapData Region(string id,string name,double x,double y,double rep,double cash,double price,bool starter,string[] types,string[] competitors)
        =>new WorldMapData {Id=id,Name=name,X=x,Y=y,ReputationThreshold=rep,CapitalThreshold=cash,PriceMultiplier=price,StartingRegion=starter,ProjectTypes=types.ToList(),CompetitorIds=competitors.ToList(),SupplierNames=new List<string>{name+" Trade Supplies"},DepotNames=new List<string>{name+" Equipment Depot"}};
    }
}
