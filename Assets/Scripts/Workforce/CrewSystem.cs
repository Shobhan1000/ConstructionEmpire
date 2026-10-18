using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.CompanyManagement;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Workforce
{
    public readonly struct WorkerHiredEvent : IGameEvent
    {
        public readonly string WorkerId;
        public WorkerHiredEvent(string workerId) { WorkerId = workerId; }
    }

    public readonly struct CrewCreatedEvent : IGameEvent
    {
        public readonly string CrewId;
        public CrewCreatedEvent(string crewId) { CrewId = crewId; }
    }

    /// <summary>
    /// Manages the employee roster and how workers are grouped into crews
    /// (spec sections 9-11). Combines "Employees" and "Crews" from the
    /// original spec into one system for now since they share almost all
    /// their state - split them apart later if the distinction grows more
    /// complex (union rules, per-worker scheduling, etc).
    /// </summary>
    public class CrewSystem
    {
        private readonly ContentLibrary _content;
        private readonly CompanySystem _companySystem;

        public List<WorkerData> Workers { get; private set; } = new List<WorkerData>();
        public List<CrewData> Crews { get; private set; } = new List<CrewData>();

        public CrewSystem(ContentLibrary content, CompanySystem companySystem)
        {
            _content = content;
            _companySystem = companySystem;

            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        public WorkerData HireWorker(WorkerType type, string name)
        {
            var def = _content.GetWorkerType(type);

            var worker = new WorkerData
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Type = type,
                Productivity = def != null ? def.BaseProductivity : 1f,
                DailyWage = def != null ? def.BaseDailyWage : 200,
                Fatigue = 0f,
                Morale = 70f
            };

            Workers.Add(worker);
            EventBus.Publish(new WorkerHiredEvent(worker.Id));
            return worker;
        }

        public bool FireWorker(string workerId)
        {
            var worker = Workers.FirstOrDefault(w => w.Id == workerId);
            if (worker == null) return false;

            foreach (var crew in Crews)
            {
                crew.WorkerIds.Remove(workerId);
            }

            return Workers.Remove(worker);
        }

        public CrewData CreateCrew(string name, IEnumerable<string> workerIds)
        {
            var crew = new CrewData
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                WorkerIds = workerIds != null ? workerIds.ToList() : new List<string>()
            };

            foreach (var workerId in crew.WorkerIds)
            {
                var worker = Workers.FirstOrDefault(w => w.Id == workerId);
                if (worker != null) worker.CurrentCrewId = crew.Id;
            }

            Crews.Add(crew);
            EventBus.Publish(new CrewCreatedEvent(crew.Id));
            return crew;
        }

        public void AssignCrewToProject(string crewId, string projectId, string phaseId)
        {
            var crew = Crews.FirstOrDefault(c => c.Id == crewId);
            if (crew == null) return;

            crew.CurrentProjectId = projectId;
            crew.CurrentPhaseId = phaseId;
        }

        public void ReleaseCrew(string crewId)
        {
            var crew = Crews.FirstOrDefault(c => c.Id == crewId);
            if (crew == null) return;

            crew.CurrentProjectId = null;
            crew.CurrentPhaseId = null;
        }

        /// <summary>Average effective productivity of the crew's workers - about 1.0 for a normal, fully-staffed, rested crew. 0 if the crew has no workers.</summary>
        public float GetCrewProductivity(string crewId)
        {
            var crew = Crews.FirstOrDefault(c => c.Id == crewId);
            if (crew == null || crew.WorkerIds.Count == 0) return 0f;

            float total = 0f;
            int counted = 0;
            foreach (var workerId in crew.WorkerIds)
            {
                var worker = Workers.FirstOrDefault(w => w.Id == workerId);
                if (worker == null) continue;
                total += worker.EffectiveProductivity();
                counted++;
            }

            return counted > 0 ? total / counted : 0f;
        }

        /// <summary>Average raw Fatigue (0-100) across the crew's workers. -1 if the crew has no workers, as a sentinel distinct from "0 fatigue".</summary>
        public float GetCrewAverageFatigue(string crewId)
        {
            var crew = Crews.FirstOrDefault(c => c.Id == crewId);
            if (crew == null || crew.WorkerIds.Count == 0) return -1f;

            float total = 0f;
            int counted = 0;
            foreach (var workerId in crew.WorkerIds)
            {
                var worker = Workers.FirstOrDefault(w => w.Id == workerId);
                if (worker == null) continue;
                total += worker.Fatigue;
                counted++;
            }

            return counted > 0 ? total / counted : -1f;
        }

        public List<WorkerData> CaptureWorkers() => Workers;
        public List<CrewData> CaptureCrews() => Crews;

        public void RestoreState(List<WorkerData> workers, List<CrewData> crews)
        {
            Workers = workers ?? new List<WorkerData>();
            Crews = crews ?? new List<CrewData>();
        }

        private void OnDayAdvanced(DayAdvancedEvent evt)
        {
            double totalWages = 0;

            foreach (var worker in Workers)
            {
                bool isWorking = !string.IsNullOrEmpty(worker.CurrentCrewId) &&
                                  Crews.Any(c => c.Id == worker.CurrentCrewId && !string.IsNullOrEmpty(c.CurrentProjectId));

                if (isWorking)
                {
                    worker.Fatigue = Math.Min(100f, worker.Fatigue + 4f);
                    totalWages += worker.DailyWage;
                }
                else
                {
                    worker.Fatigue = Math.Max(0f, worker.Fatigue - 8f);
                }

                // Morale drifts toward a neutral 70 when nothing else is actively moving it.
                worker.Morale += (70f - worker.Morale) * 0.05f;
            }

            if (totalWages > 0)
            {
                _companySystem.TrySpendCash(totalWages, "Daily wages");
            }
        }
    }
}
