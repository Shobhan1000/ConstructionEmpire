using System;
using System.Linq;
using System.Collections.Generic;
using ConstructionEmpire.Data.Procurement;
namespace ConstructionEmpire.Systems.Management
{
    public enum SupplyPreference { Reliability,LowestCost,Fastest }
    public sealed class SupplyPlanLine
    {public string MaterialId,SupplierId,SupplierName;public double Quantity,Cost;public int LeadHours;public double Reliability;}
    public sealed class SupplyPlan
    {public readonly List<SupplyPlanLine> Lines=new List<SupplyPlanLine>();public readonly List<string> Problems=new List<string>();public double Total=>Lines.Sum(x=>x.Cost);}
    public static class SupplyPlanner
    {
        public static SupplyPlan Preview(ManagementSession session,string projectId,SupplyPreference preference)
        {
            if(!Enum.IsDefined(typeof(SupplyPreference),preference))throw new ArgumentException("Choose a valid supplier policy.");
            if(session.Find(projectId)==null)throw new ArgumentException("Choose a project.");var result=new SupplyPlan();
            foreach(var group in session.Phases(projectId).Where(p=>!p.IsComplete).SelectMany(p=>p.MaterialRequirements.Select(m=>new {m.MaterialId,Quantity=m.Quantity*(1-p.Progress/100)})).GroupBy(x=>x.MaterialId).OrderBy(x=>x.Key,StringComparer.Ordinal))
            {
                double quantity=Math.Max(0,Math.Round(group.Sum(x=>x.Quantity)-session.Inventory.GetQuantity(group.Key)-session.Orders.Where(o=>!o.Delivered && o.MaterialId==group.Key).Sum(o=>o.Quantity),6));if(quantity<=.000001)continue;
                var choices=new List<SupplyPlanLine>();
                foreach(var supplier in session.Suppliers.ForMaterial(group.Key))
                {
                    try{var q=session.QuoteMaterial(projectId,supplier.Id,group.Key,quantity,ShippingMethod.Standard);choices.Add(new SupplyPlanLine {MaterialId=group.Key,SupplierId=supplier.Id,SupplierName=supplier.Name,Quantity=quantity,Cost=q.Total,LeadHours=q.LeadHours,Reliability=supplier.Reliability});}
                    catch(InvalidOperationException){/* Capacity unavailable; include this material in the explicit problem list if no supplier can fulfill it. */}
                }
                var selected=(preference==SupplyPreference.LowestCost?choices.OrderBy(x=>x.Cost):preference==SupplyPreference.Fastest?choices.OrderBy(x=>x.LeadHours):choices.OrderByDescending(x=>x.Reliability)).ThenBy(x=>x.Cost).ThenBy(x=>x.SupplierId,StringComparer.Ordinal).FirstOrDefault();
                if(selected==null)result.Problems.Add("No supplier can fulfill "+quantity.ToString("0.##")+" units of "+group.Key+" in one order.");else result.Lines.Add(selected);
            }
            return result;
        }
        public static int Purchase(ManagementSession session,string projectId,SupplyPreference preference,double approvedMaximum)
        {
            if(double.IsNaN(approvedMaximum) || double.IsInfinity(approvedMaximum) || approvedMaximum<0)throw new ArgumentException("Invalid approved spending limit.");
            var plan=Preview(session,projectId,preference);
            if(plan.Problems.Count>0)throw new InvalidOperationException(string.Join(" ",plan.Problems));
            if(plan.Total>approvedMaximum+.000001)throw new InvalidOperationException("Prices changed. Review the supply plan again.");
            if((decimal)plan.Total>session.Finance.CashFlow.Cash)throw new InvalidOperationException("Insufficient cash for this supply plan. No orders were placed.");
            foreach(var line in plan.Lines)session.OrderMaterial(projectId,line.MaterialId,line.Quantity,line.SupplierId);
            return plan.Lines.Count;
        }
    }
}
