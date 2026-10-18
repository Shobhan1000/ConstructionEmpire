using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Clients
{
    public readonly struct ClientRelationshipChangedEvent : IGameEvent
    {
        public readonly string ClientId;
        public readonly float NewRelationship;
        public ClientRelationshipChangedEvent(string clientId, float newRelationship)
        {
            ClientId = clientId;
            NewRelationship = newRelationship;
        }
    }

    /// <summary>
    /// A persistent roster of clients the company does repeat business
    /// with (spec sections 30-31). Seeded once at startup rather than
    /// generated per-project - relationship only means something if it's
    /// the same client coming back.
    /// </summary>
    public class ClientSystem
    {
        private readonly SeededRandom _random;

        public List<ClientData> Clients { get; private set; } = new List<ClientData>();

        private static readonly (string Name, ClientType Type, float PriceSensitivity, QualityLevel QualityExpectation)[] SeedRoster =
        {
            ("Harlow Residence", ClientType.PrivateHomeowner, 0.7f, QualityLevel.Average),
            ("The Marlowe Family", ClientType.PrivateHomeowner, 0.6f, QualityLevel.Average),
            ("Kestrel Property Group", ClientType.PropertyDeveloper, 0.8f, QualityLevel.Average),
            ("Oakview Developments", ClientType.PropertyDeveloper, 0.75f, QualityLevel.Average),
            ("Bramwell & Co.", ClientType.SmallBusiness, 0.65f, QualityLevel.Good),
            ("Riverside Holdings", ClientType.Corporation, 0.4f, QualityLevel.Good),
            ("Percy Street Partners", ClientType.SmallBusiness, 0.7f, QualityLevel.Average),
            ("Ashford Municipal Works", ClientType.Municipality, 0.3f, QualityLevel.Good)
        };

        public ClientSystem(SeededRandom random)
        {
            _random = random;

            foreach (var seed in SeedRoster)
            {
                Clients.Add(new ClientData
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = seed.Name,
                    Type = seed.Type,
                    PriceSensitivity = seed.PriceSensitivity,
                    QualityExpectation = seed.QualityExpectation,
                    RelationshipScore = 40f
                });
            }
        }

        public ClientData GetClient(string clientId) => clientId != null ? Clients.FirstOrDefault(c => c.Id == clientId) : null;

        /// <summary>Weighted toward higher-relationship clients, so repeat business happens naturally rather than every client being equally likely every time (spec section 31).</summary>
        public ClientData GetRandomClient()
        {
            if (Clients.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var c in Clients) totalWeight += c.RelationshipScore + 10f;

            float roll = (float)(_random.NextDouble() * totalWeight);
            float cumulative = 0f;

            foreach (var c in Clients)
            {
                cumulative += c.RelationshipScore + 10f;
                if (roll <= cumulative) return c;
            }

            return Clients[Clients.Count - 1];
        }

        public void AdjustRelationship(string clientId, float delta, string reason)
        {
            var client = GetClient(clientId);
            if (client == null) return;

            float newScore = Math.Max(0f, Math.Min(100f, client.RelationshipScore + delta));
            client.RelationshipScore = newScore;

            EventBus.Publish(new ClientRelationshipChangedEvent(clientId, newScore));
        }

        public List<ClientData> CaptureState() => Clients;

        public void RestoreState(List<ClientData> clients)
        {
            Clients = clients ?? new List<ClientData>();
        }
    }
}
