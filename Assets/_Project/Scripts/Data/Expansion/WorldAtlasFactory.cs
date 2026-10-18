using System;
using System.Collections.Generic;
using System.Linq;
namespace ConstructionEmpire.Data.Expansion
{
    public sealed class CareerOptions
    {
        public string CompanyName,RegionId="uk-london";
        public int Seed=12345;
        public float StartingCash=-1;
    }
    /// <summary>Authored game balance, not real-world economic or currency data.</summary>
    public static class WorldAtlasFactory
    {
        public static List<WorldMapData> Create()
        {
            var result=new List<WorldMapData>();
            Add(result,"uk","United Kingdom",new[]{"London","Manchester","Bristol"},0,1.05,1.12,"Urban refurbishment, office conversions and residential extensions.");
            Add(result,"us","United States",new[]{"Chicago","Austin","Seattle"},1000,1.12,1.22,"Warehousing, commercial fit-outs and growing regional markets.");
            Add(result,"ca","Canada",new[]{"Toronto","Calgary","Vancouver"},2000,1.08,1.18,"Mixed residential work, distribution facilities and office investment.");
            Add(result,"au","Australia",new[]{"Melbourne","Brisbane","Sydney"},3000,1.15,1.27,"Residential upgrades, hospitality and premium commercial work.");
            return result;
        }
        private static void Add(List<WorldMapData> result,string id,string country,string[] cities,int offset,double cost,double price,string description)
        {
            for(int i=0;i<cities.Length;i++)result.Add(new WorldMapData {
                Id=id+"-"+cities[i].ToLowerInvariant(),Name=cities[i],CountryId=id,CountryName=country,MarketDescription=description,
                X=offset+40+i*90,Y=40+(i%2)*90,OperatingCostMultiplier=cost+(i-1)*.08,PriceMultiplier=price+(i-1)*.07,
                ReputationThreshold=50+i*12,CapitalThreshold=150000+i*200000,UnlockRule=i==2?RegionUnlockRule.ReputationAndCapital:RegionUnlockRule.ReputationOrCapital,
                ProjectTypes=new List<string>{"Residential","Warehouse","Hospitality","Office"},PreferredCategoryWeight=2,PreferredProjectTypes=(i==0?new[]{"Residential","Warehouse","Hospitality","Office"}:i==1?new[]{"Residential","Warehouse"}:new[]{"Hospitality","Office"}).ToList(),
                CompetitorIds=new List<string>{"competitor-1","competitor-2","competitor-3","competitor-4","competitor-5"},SupplierNames=new List<string>{cities[i]+" Trade Hub"},DepotNames=new List<string>{cities[i]+" Equipment Depot"}
            });
        }
    }
}
