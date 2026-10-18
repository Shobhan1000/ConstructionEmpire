using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Clients;
using ConstructionEmpire.Core;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Tests
{
    public class ClientSystemTests
    {
        private ClientSystem _clients;

        [SetUp]
        public void SetUp()
        {
            _clients = new ClientSystem(new SeededRandom(5));
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        [Test]
        public void Constructor_SeedsARosterOfClients()
        {
            Assert.Greater(_clients.Clients.Count, 0);
            Assert.IsTrue(_clients.Clients.All(c => !string.IsNullOrEmpty(c.Id) && !string.IsNullOrEmpty(c.Name)));
        }

        [Test]
        public void GetClient_ForKnownId_ReturnsThatClient()
        {
            var expected = _clients.Clients.First();
            var found = _clients.GetClient(expected.Id);
            Assert.AreSame(expected, found);
        }

        [Test]
        public void GetClient_ForUnknownId_ReturnsNull()
        {
            Assert.IsNull(_clients.GetClient("does-not-exist"));
        }

        [Test]
        public void AdjustRelationship_ClampsAtUpperBound()
        {
            var client = _clients.Clients.First();
            _clients.AdjustRelationship(client.Id, 1000f, "test");
            Assert.AreEqual(100f, client.RelationshipScore);
        }

        [Test]
        public void AdjustRelationship_ClampsAtLowerBound()
        {
            var client = _clients.Clients.First();
            _clients.AdjustRelationship(client.Id, -1000f, "test");
            Assert.AreEqual(0f, client.RelationshipScore);
        }

        [Test]
        public void GetRandomClient_FavorsHigherRelationshipClientsOverManyDraws()
        {
            var favored = _clients.Clients.First();
            var neglected = _clients.Clients.Last();

            favored.RelationshipScore = 100f;
            neglected.RelationshipScore = 0f;

            int favoredCount = 0;
            int neglectedCount = 0;

            for (int i = 0; i < 300; i++)
            {
                var picked = _clients.GetRandomClient();
                if (picked == favored) favoredCount++;
                else if (picked == neglected) neglectedCount++;
            }

            Assert.Greater(favoredCount, neglectedCount);
        }
    }
}
