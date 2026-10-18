using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Content;

namespace ConstructionEmpire.Projects
{
    public class PhaseScheduleInfo
    {
        public string PhaseId;
        public int DurationDays;
        public int EarliestStart;
        public int EarliestFinish;
        public int LatestStart;
        public int LatestFinish;
        public int Slack => LatestStart - EarliestStart;
        public bool IsCritical => Slack == 0;
    }

    /// <summary>
    /// Classic critical-path-method scheduling over a project's phases,
    /// using the same PrerequisitePhaseIds data ConstructionProjectSystem
    /// already uses for dependency gating (spec section 8). Pure
    /// calculation, day 0 is the project's start date - a future Gantt UI
    /// can render this directly. ConstructionProjectSystem does not use
    /// this for gameplay logic; actual progress is still driven by real
    /// crew/equipment/material/weather factors, not this idealized
    /// schedule.
    /// </summary>
    public static class CriticalPathCalculator
    {
        public static List<PhaseScheduleInfo> Calculate(ContentLibrary content, IEnumerable<string> phaseIds)
        {
            var ids = phaseIds.ToList();
            var infos = new Dictionary<string, PhaseScheduleInfo>();

            foreach (var id in ids)
            {
                var def = content.GetPhase(id);
                infos[id] = new PhaseScheduleInfo
                {
                    PhaseId = id,
                    DurationDays = def != null ? def.BaseDurationDays : 1
                };
            }

            if (ids.Count == 0) return new List<PhaseScheduleInfo>();

            RunForwardPass(content, ids, infos);

            int projectDuration = infos.Values.Max(i => i.EarliestFinish);

            RunBackwardPass(content, ids, infos, projectDuration);

            return ids.Select(id => infos[id]).ToList();
        }

        /// <summary>Earliest start/finish for every phase. Iterates to a fixed point rather than requiring a pre-sorted topological order - simple and correct for the small phase counts this game deals with.</summary>
        private static void RunForwardPass(ContentLibrary content, List<string> ids, Dictionary<string, PhaseScheduleInfo> infos)
        {
            bool changed = true;
            int safety = 0;
            int safetyLimit = (ids.Count * ids.Count) + 10;

            while (changed && safety < safetyLimit)
            {
                changed = false;
                safety++;

                foreach (var id in ids)
                {
                    var def = content.GetPhase(id);
                    var prerequisites = (def != null ? def.PrerequisitePhaseIds : new string[0]) ?? new string[0];

                    int earliestStart = 0;
                    foreach (var prereqId in prerequisites)
                    {
                        if (infos.TryGetValue(prereqId, out var prereqInfo))
                        {
                            earliestStart = Math.Max(earliestStart, prereqInfo.EarliestFinish);
                        }
                    }

                    var info = infos[id];
                    int earliestFinish = earliestStart + info.DurationDays;

                    if (earliestStart != info.EarliestStart || earliestFinish != info.EarliestFinish)
                    {
                        info.EarliestStart = earliestStart;
                        info.EarliestFinish = earliestFinish;
                        changed = true;
                    }
                }
            }
        }

        /// <summary>Latest start/finish for every phase, given the overall project duration from the forward pass.</summary>
        private static void RunBackwardPass(ContentLibrary content, List<string> ids, Dictionary<string, PhaseScheduleInfo> infos, int projectDuration)
        {
            foreach (var info in infos.Values)
            {
                info.LatestFinish = projectDuration;
                info.LatestStart = projectDuration - info.DurationDays;
            }

            bool changed = true;
            int safety = 0;
            int safetyLimit = (ids.Count * ids.Count) + 10;

            while (changed && safety < safetyLimit)
            {
                changed = false;
                safety++;

                foreach (var id in ids)
                {
                    var dependents = ids.Where(otherId =>
                    {
                        var otherDef = content.GetPhase(otherId);
                        var prereqs = otherDef != null ? otherDef.PrerequisitePhaseIds : null;
                        return prereqs != null && prereqs.Contains(id);
                    });

                    int latestFinish = projectDuration;
                    foreach (var dependentId in dependents)
                    {
                        if (infos.TryGetValue(dependentId, out var dependentInfo))
                        {
                            latestFinish = Math.Min(latestFinish, dependentInfo.LatestStart);
                        }
                    }

                    var info = infos[id];
                    int latestStart = latestFinish - info.DurationDays;

                    if (latestFinish != info.LatestFinish || latestStart != info.LatestStart)
                    {
                        info.LatestFinish = latestFinish;
                        info.LatestStart = latestStart;
                        changed = true;
                    }
                }
            }
        }
    }
}
