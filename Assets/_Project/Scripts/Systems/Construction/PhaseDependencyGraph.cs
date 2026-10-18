using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Systems.Construction
{
    public sealed class PhaseDependencyGraph
    {
        private readonly Dictionary<string,ConstructionPhaseRuntimeData> phases;
        private readonly List<string> order = new List<string>();
        public IReadOnlyList<string> TopologicalOrder => order.AsReadOnly();
        public PhaseDependencyGraph(IEnumerable<ConstructionPhaseRuntimeData> source)
        {
            if(source==null) throw new ArgumentNullException(nameof(source));
            phases=new Dictionary<string,ConstructionPhaseRuntimeData>(StringComparer.Ordinal);
            foreach(var phase in source)
            {
                if(phase==null) throw new ArgumentException("Null phase."); phase.Validate();
                if(phases.ContainsKey(phase.Id)) throw new ArgumentException("Duplicate phase ID: "+phase.Id);
                phases.Add(phase.Id,phase);
            }
            if(phases.Count==0) throw new ArgumentException("A project needs phases.");
            var visiting=new HashSet<string>(); var done=new HashSet<string>();
            foreach(var id in phases.Keys.OrderBy(x=>x,StringComparer.Ordinal)) Visit(id,visiting,done);
            foreach(var phase in phases.Values)
                if(phase.Progress>0 && phase.PrerequisiteIds.Any(id=>!phases[id].IsComplete)) throw new ArgumentException("Progress exists before prerequisite completion: "+phase.Id);
        }
        private void Visit(string id,HashSet<string> visiting,HashSet<string> done)
        {
            if(!phases.ContainsKey(id)) throw new ArgumentException("Missing prerequisite: "+id);
            if(done.Contains(id)) return;
            if(!visiting.Add(id)) throw new ArgumentException("Cyclic phase dependency at: "+id);
            foreach(var prerequisite in phases[id].PrerequisiteIds.OrderBy(x=>x,StringComparer.Ordinal)) Visit(prerequisite,visiting,done);
            visiting.Remove(id); done.Add(id); order.Add(id);
        }
        public bool CanStart(string id)
        {
            if(!phases.TryGetValue(id,out var phase)) throw new ArgumentException("Unknown phase: "+id);
            return !phase.IsComplete && phase.PrerequisiteIds.All(x=>phases[x].IsComplete);
        }
        public IReadOnlyList<ConstructionPhaseRuntimeData> EligiblePhases() => order.Where(CanStart).Select(id=>phases[id]).ToList().AsReadOnly();
    }
}
