using System;
using System.Collections.Generic;
using ConstructionEmpire.Clients;
using ConstructionEmpire.Content;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Projects
{
    public readonly struct ProjectOpportunitiesGeneratedEvent : IGameEvent
    {
        public readonly int Count;
        public ProjectOpportunitiesGeneratedEvent(int count) { Count = count; }
    }

    /// <summary>Creates new project opportunities from the available templates (spec sections 2 and 56).</summary>
    public class ProjectGenerationSystem
    {
        private readonly ContentLibrary _content;
        private readonly SeededRandom _random;
        private readonly ClientSystem _clientSystem;

        // Fallback names only used if no ClientSystem is wired in (e.g. a
        // minimal test setup) - normal play always pulls a real client from
        // ClientSystem so relationship tracking has something to attach to.
        private static readonly string[] FallbackClientNames =
        {
            "Harlow Residence", "Kestrel Property Group", "Bramwell & Co.",
            "Riverside Holdings", "Oakview Developments", "The Marlowe Family",
            "Percy Street Partners", "Ashford Municipal Works"
        };

        public ProjectGenerationSystem(ContentLibrary content, SeededRandom random, ClientSystem clientSystem = null)
        {
            _content = content;
            _random = random;
            _clientSystem = clientSystem;
        }

        public List<ProjectRuntimeData> GenerateOpportunities(int count, DateTime currentDate)
        {
            var results = new List<ProjectRuntimeData>();
            var templates = _content.AllProjectTemplates;
            if (templates.Count == 0) return results;

            for (int i = 0; i < count; i++)
            {
                var template = templates[_random.NextInt(0, templates.Count)];
                results.Add(BuildFromTemplate(template, currentDate));
            }

            EventBus.Publish(new ProjectOpportunitiesGeneratedEvent(results.Count));
            return results;
        }

        private ProjectRuntimeData BuildFromTemplate(ProjectTemplate template, DateTime currentDate)
        {
            double contractValue = _random.NextFloat((float)template.MinContractValue, (float)template.MaxContractValue);
            int totalDurationDays = 0;

            var phases = new List<PhaseRuntimeData>();
            foreach (var phaseId in template.PhaseIds)
            {
                var phaseDef = _content.GetPhase(phaseId);
                if (phaseDef == null) continue;

                totalDurationDays += phaseDef.BaseDurationDays;
                phases.Add(new PhaseRuntimeData
                {
                    PhaseId = phaseDef.PhaseId,
                    DisplayName = phaseDef.DisplayName,
                    Status = PhaseStatus.Locked
                });
            }

            // Bidding + mobilization window on top of the raw build time.
            int biddingWindowDays = _random.NextInt(5, 15);
            var deadline = currentDate.AddDays(totalDurationDays + biddingWindowDays);

            var client = _clientSystem?.GetRandomClient();

            return new ProjectRuntimeData
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = template.Type,
                DisplayName = template.DisplayName,
                ClientId = client?.Id,
                ClientName = client != null ? client.Name : FallbackClientNames[_random.NextInt(0, FallbackClientNames.Length)],
                ContractValue = contractValue,
                DeadlineIso = deadline.ToString("o"),
                Status = ProjectStatus.Available,
                Phases = phases
            };
        }
    }
}
