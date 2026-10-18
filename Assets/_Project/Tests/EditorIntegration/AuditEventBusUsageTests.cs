using ConstructionEmpire.Editor;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditorIntegration
{
    public sealed class AuditEventBusUsageTests
    {
        [Test]
        public void FindsQualifiedMultilineAndMultipleDeclarations()
        {
            var found = AuditEventBusUsage.FindLegacyDeclarations(
                "class Example {\n public event global::System.Action<int, string>\n OnBalanceChanged, ProjectStartedEvent;\n" +
                "event Action DailyTick { add {} remove {} } }");
            Assert.That(found.Count, Is.EqualTo(3));
            Assert.That(found[0].Line, Is.EqualTo(2));
            Assert.That(found[0].EventName, Is.EqualTo("OnBalanceChanged"));
        }

        [Test]
        public void IgnoresCommentsStringsCallbacksAndUnrelatedEvents()
        {
            var found = AuditEventBusUsage.FindLegacyDeclarations(
                "// event Action BalanceChanged;\n/* event Action BidWon; */\n" +
                "string s = \"event Action BidLost;\";\n" +
                "Action<BalanceChangedEvent> handler; event Action Other; GameEvents.Subscribe(handler);");
            Assert.That(found, Is.Empty);
        }
    }
}
