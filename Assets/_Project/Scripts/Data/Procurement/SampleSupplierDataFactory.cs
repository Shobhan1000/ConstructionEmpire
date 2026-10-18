using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Data.Procurement
{
    public static class SampleSupplierDataFactory
    {
        public static List<SupplierData> CreateStarterSuppliers()
        {
            string[] names={"Stonehaven Ready-Mix","BudgetBatch Concrete","Cedarline Timber","Bargain Beam Yard","Ironcrest Steel","QuickSpan Metals","Northbridge Building Supply","Anchor Hardware & Materials","Pennywise Builders Depot","Crownline Trade Supply"};
            string[] personalities={"Premium concrete with dependable delivery.","Low-cost concrete with occasional dispatch delays.","Consistent lumber quality and reliable lead times.","Discount timber for planners with flexible schedules.","Premium steel, steady quality and punctual dispatch.","Competitive steel prices with variable availability.","Local general supplies with dependable service.","National hardware range and predictable deliveries.","Low prices across general materials, but occasional delays.","Premium general materials with guaranteed on-time dispatch."};
            double[] reliability={.98,.65,.97,.7,.99,.8,.96,.98,.6,1};
            double[] price={1.15,.85,1.1,.8,1.2,.9,1,1.05,.82,1.25};
            var catalog=ManagementSettings.CreateStarter().Materials;var result=new List<SupplierData>();
            for(int i=0;i<10;i++)
            {
                var supplier=new SupplierData {Id="supplier-"+(i+1),Name=names[i],Personality=personalities[i],Reliability=reliability[i]};
                var materials=i<2 ? catalog.Where(x=>new[]{"concrete","cement","sand","gravel"}.Contains(x.Id)) : i<4 ? catalog.Where(x=>x.Id=="timber") : i<6 ? catalog.Where(x=>x.Id=="steel") : catalog;
                foreach(var m in materials) supplier.Offers.Add(new SupplierOffer {MaterialId=m.Id,BasePrice=m.Price*price[i],CurrentPrice=m.Price*price[i],LeadHours=i==9 ? 4 : 4+(i%3)*4,Quality=i==9 ? 1 : .75+reliability[i]*.2,Volatility=.01+(1-reliability[i])*.1,Capacity=i==7 ? 20000 : 10000});
                supplier.Validate();result.Add(supplier);
            }
            return result;
        }
    }
}
