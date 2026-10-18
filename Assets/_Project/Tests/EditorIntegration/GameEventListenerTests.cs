using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Data.Events;
using NUnit.Framework;
using UnityEngine;

namespace ConstructionEmpire.Tests.EditorIntegration
{
    [ExecuteAlways] // Exercise Unity lifecycle callbacks in the edit-mode test scene.
    public sealed class ProbeEventListener : GameEventListener
    {
        public static int Calls;
        protected override void RegisterEvents(EventSubscriptions subscriptions) =>
            subscriptions.Subscribe<ProjectStartedEvent>(payload => Calls++);
    }

    public sealed class GameEventListenerTests
    {
        [Test]
        public void DisableReenableAndDestroy_CleanUpSubscriptions()
        {
            GameEvents.Clear();
            ProbeEventListener.Calls = 0;
            var owner = new GameObject("Listener test");
            try
            {
                var listener = owner.AddComponent<ProbeEventListener>();
                GameEvents.Publish(new ProjectStartedEvent("P1"));
                Assert.That(ProbeEventListener.Calls, Is.EqualTo(1));
                listener.enabled = false;
                GameEvents.Publish(new ProjectStartedEvent("P1"));
                Assert.That(ProbeEventListener.Calls, Is.EqualTo(1));
                listener.enabled = true;
                GameEvents.Publish(new ProjectStartedEvent("P1"));
                Assert.That(ProbeEventListener.Calls, Is.EqualTo(2));
                Object.DestroyImmediate(owner);
                GameEvents.Publish(new ProjectStartedEvent("P1"));
                Assert.That(ProbeEventListener.Calls, Is.EqualTo(2));
            }
            finally
            {
                if (owner != null) Object.DestroyImmediate(owner);
                GameEvents.Clear();
            }
        }
    }
}
