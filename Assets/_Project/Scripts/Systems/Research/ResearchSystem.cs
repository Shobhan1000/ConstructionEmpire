using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Research;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Research
{
    /// <summary>Plain research state machine. Effects are rebuilt from unlocked IDs, never compounded on previous results.</summary>
    public sealed class ResearchSystem : IRegisterableSystem
    {
        private readonly Dictionary<string,ResearchUpgradeData> tree;
        private readonly HashSet<string> unlocked=new HashSet<string>(),available=new HashSet<string>();
        private readonly Func<decimal> cash;private readonly Action<decimal,string> pay;private readonly Action apply;
        private bool purchasing;private long lastHour;
        public string ActiveId {get;private set;} public long CompletionHour {get;private set;} public int CompanyLevel {get;private set;}
        public IReadOnlyList<ResearchUpgradeData> Upgrades=>tree.Values.Select(x=>x.Copy()).ToList().AsReadOnly();
        public ResearchSystem(IEnumerable<ResearchUpgradeData> definitions,Func<decimal> cash,Action<decimal,string> pay,Action apply=null,int companyLevel=1)
        {
            if(definitions==null || cash==null || pay==null)throw new ArgumentNullException();if(companyLevel<1)throw new ArgumentOutOfRangeException(nameof(companyLevel));
            var list=definitions.ToList();foreach(var x in list){if(x==null)throw new ArgumentException("Null research.");x.Validate();}
            if(list.Select(x=>x.Id).Distinct().Count()!=list.Count)throw new ArgumentException("Duplicate research ID.");
            tree=list.ToDictionary(x=>x.Id,x=>x.Copy());this.cash=cash;this.pay=pay;this.apply=apply;CompanyLevel=companyLevel;
            var visiting=new HashSet<string>();var visited=new HashSet<string>();foreach(var id in tree.Keys)Visit(id,visiting,visited);
        }
        private void Visit(string id,HashSet<string> visiting,HashSet<string> visited)
        {if(visited.Contains(id))return;if(!tree.ContainsKey(id) || !visiting.Add(id))throw new ArgumentException("Research prerequisites contain a cycle or missing ID: "+id);foreach(var p in tree[id].PrerequisiteIds)Visit(p,visiting,visited);visiting.Remove(id);visited.Add(id);}
        public bool IsUnlocked(string id)=>unlocked.Contains(id);
        public double Modifier(ResearchEffectKind kind)=>tree.Values.Where(x=>unlocked.Contains(x.Id)).SelectMany(x=>x.Effects).Where(x=>x.Kind==kind).Sum(x=>x.Amount);
        public string BlockReason(string id)
        {
            if(!tree.TryGetValue(id,out var x))return "Unknown upgrade";
            if(IsUnlocked(id))return "Completed";if(ActiveId!=null)return ActiveId==id?"Research in progress":"Another upgrade is in progress";
            if(CompanyLevel<x.MinimumCompanyLevel)return "Requires company level "+x.MinimumCompanyLevel;
            if(x.PrerequisiteIds.Any(p=>!IsUnlocked(p)))return "Complete prerequisites first";
            if(cash()<(decimal)x.Cost)return "Insufficient cash";return null;
        }
        public void Start(string id,long hour)
        {
            if(hour<lastHour || purchasing)throw new InvalidOperationException("Invalid research start.");var reason=BlockReason(id);if(reason!=null)throw new InvalidOperationException(reason);
            long due=checked(hour+tree[id].DurationHours);purchasing=true;ActiveId=id;CompletionHour=due;
            try{pay((decimal)tree[id].Cost,id);}catch{ActiveId=null;CompletionHour=0;throw;}finally{purchasing=false;}
            RefreshAvailability();GameEvents.Publish(new ResearchChangedEvent(this,id,"Started"));
        }
        public void SetCompanyLevel(int level){if(level<1)throw new ArgumentOutOfRangeException(nameof(level));CompanyLevel=level;RefreshAvailability();GameEvents.Publish(new ResearchChangedEvent(this,null,"Company level changed"));}
        public void RefreshAvailability()
        {if(purchasing)return;foreach(var id in tree.Keys){bool ready=BlockReason(id)==null;if(ready && available.Add(id))GameEvents.Publish(new ResearchAvailableEvent(this,id));else if(!ready)available.Remove(id);}}
        public void OnTick(TickContext context)
        {
            if(context.TotalHours<=lastHour)return;lastHour=context.TotalHours;
            if(ActiveId!=null && lastHour>=CompletionHour){string id=ActiveId;unlocked.Add(id);ActiveId=null;apply?.Invoke();GameEvents.Publish(new ResearchChangedEvent(this,id,"Completed"));}
            else if(ActiveId!=null)GameEvents.Publish(new ResearchChangedEvent(this,ActiveId,"Progress"));
            RefreshAvailability();
        }
    }
}
