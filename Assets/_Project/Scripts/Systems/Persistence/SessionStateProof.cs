using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ConstructionEmpire.Systems.Management;
namespace ConstructionEmpire.Systems.Persistence
{
    /// <summary>Deterministic verification projection; UI-only site layout projections are excluded.</summary>
    public static class SessionStateProof
    {
        public static string Compute(ManagementSession s)
        {
            var projects=s.Projects.Select(x=>{var p=x.Copy();p.SiteMap=null;return p;}).ToList();
            var research=s.Research.System;
            var state=new SortedDictionary<string,object>(StringComparer.Ordinal)
            {
                {"changeOrders",s.ChangeOrders.Orders},{"development",new object[]{s.Development.Level,s.Development.Clients}},{"hour",s.Simulation.Time.TotalHours},{"randomCalls",s.Simulation.Random.Calls},{"company",s.Simulation.Company},
                {"finance",s.Finance.State},{"projects",projects},{"phases",s.Projects.Select(x=>s.Phases(x.Id).ToList()).ToList()},
                {"workforce",s.Workforce},{"crews",s.Expansion.Crews},{"inventory",s.Inventory},{"orders",s.Orders},
                {"suppliers",s.Suppliers.Suppliers},{"subcontractors",s.Subcontractors.Companies},
                {"bookings",s.Scheduling.Bookings.Select(x=>x.Data).ToList()},
                {"offices",s.Expansion.Offices},{"competitors",s.Competitors},{"market",s.Market.Current},
                {"weather",s.Projects.Select(x=>x.Region).Distinct().OrderBy(x=>x,StringComparer.Ordinal).Select(x=>s.Risks.Weather.Get(x)).ToList()},
                {"research",new object[]{research.ActiveId,research.CompletionHour,research.CompanyLevel,research.Upgrades.Where(x=>research.IsUnlocked(x.Id)).Select(x=>x.Id).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),s.Research.ProcurementEnabled,s.Research.SchedulingEnabled}}
            };
            var text=new StringBuilder();Write(state,text,0);return Hash(text.ToString());
        }
        public static string Hash(string text){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        private static void Write(object value,StringBuilder target,int depth)
        {
            if(depth>64)throw new InvalidOperationException("State projection exceeded its nesting limit.");
            if(value==null){target.Append("null;");return;}
            var type=value.GetType();
            if(value is string text){target.Append(text.Length).Append(':').Append(text).Append(';');return;}
            if(type.IsEnum || type.IsPrimitive || value is decimal){target.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
            if(value is IDictionary map){target.Append('{');foreach(var key in map.Keys.Cast<object>().OrderBy(x=>Convert.ToString(x,CultureInfo.InvariantCulture),StringComparer.Ordinal)){Write(key,target,depth+1);Write(map[key],target,depth+1);}target.Append('}');return;}
            if(value is IEnumerable list){target.Append('[');foreach(var item in list)Write(item,target,depth+1);target.Append(']');return;}
            target.Append(type.FullName).Append('{');
            foreach(var member in type.GetMembers(BindingFlags.Public|BindingFlags.Instance).Where(m=>m is FieldInfo || m is PropertyInfo p && p.CanRead && p.GetIndexParameters().Length==0).OrderBy(m=>m.Name,StringComparer.Ordinal))
            {target.Append(member.Name).Append('=');Write(member is FieldInfo field?field.GetValue(value):((PropertyInfo)member).GetValue(value),target,depth+1);}
            target.Append('}');
        }
    }
}
