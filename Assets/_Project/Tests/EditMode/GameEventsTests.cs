using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Simulation;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class GameEventsTests
    {
        [SetUp] public void SetUp() => GameEvents.Clear();
        [TearDown] public void TearDown() => GameEvents.Clear();

        [Test]
        public void SubscribePublishUnsubscribe_DeliversTypedPayloadOnce()
        {
            int calls = 0;
            Action<ProjectStartedEvent> listener = e => { calls++; Assert.That(e.ProjectId, Is.EqualTo("P1")); };
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            GameEvents.Subscribe(listener);
            GameEvents.Subscribe(listener);
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            GameEvents.Unsubscribe(listener);
            GameEvents.Unsubscribe(listener);
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void PayloadTypes_AreIsolated()
        {
            int calls = 0;
            GameEvents.Subscribe<ProjectCompletedEvent>(e => calls++);
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void SubscriptionChanges_UseSnapshotAndRegistrationOrder()
        {
            var calls = new List<string>();
            Action<ProjectStartedEvent> second = e => calls.Add("B");
            Action<ProjectStartedEvent> third = e => calls.Add("C");
            GameEvents.Subscribe<ProjectStartedEvent>(e =>
            {
                calls.Add("A"); GameEvents.Unsubscribe(second); GameEvents.Subscribe(third);
            });
            GameEvents.Subscribe(second);
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            Assert.That(calls, Is.EqualTo(new[] { "A", "B", "A", "C" }));
        }

        [Test]
        public void DisposeScope_SuppressesEvenAnInFlightCallback()
        {
            int calls = 0;
            var scope = new EventSubscriptions();
            GameEvents.Subscribe<ProjectStartedEvent>(e => scope.Dispose());
            scope.Subscribe<ProjectStartedEvent>(e => calls++);
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            scope.Dispose();
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            Assert.That(calls, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => scope.Subscribe<ProjectStartedEvent>(e => {}));
        }

        [Test]
        public void IndependentScopes_DoNotRemoveEachOthersHandlers()
        {
            int calls = 0;
            Action<ProjectStartedEvent> handler = e => calls++;
            using (var first = new EventSubscriptions())
            using (var second = new EventSubscriptions())
            {
                first.Subscribe(handler); second.Subscribe(handler);
                first.Dispose(); GameEvents.Publish(new ProjectStartedEvent("P1"));
            }
            GameEvents.Publish(new ProjectStartedEvent("P1"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Clear_RemovesPreviousSessionListeners()
        {
            int calls = 0;
            GameEvents.Subscribe<ProjectStartedEvent>(e => calls++);
            GameEvents.Clear(); GameEvents.Publish(new ProjectStartedEvent("P1"));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void NullHandlers_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GameEvents.Subscribe<ProjectStartedEvent>(null));
            Assert.Throws<ArgumentNullException>(() => GameEvents.Unsubscribe<ProjectStartedEvent>(null));
        }

        [Test]
        public void ListenerFailure_PropagatesToDriver()
        {
            GameEvents.Subscribe<ProjectStartedEvent>(e => throw new InvalidOperationException("failure"));
            Assert.Throws<InvalidOperationException>(() => GameEvents.Publish(new ProjectStartedEvent("P1")));
        }

        private sealed class Counter : IRegisterableSystem
        {
            public int Calls;
            public void OnTick(TickContext context) => Calls++;
        }

        [Test]
        public void Managers_OnlyDriveTheirOwnClock_AndUnsubscribeOnDispose()
        {
            using (var first = new SimulationManager(new CompanyData("A", 100), new GameTimeData(), 1))
            using (var second = new SimulationManager(new CompanyData("B", 100), new GameTimeData(), 2))
            {
                var a = new Counter(); var b = new Counter();
                first.Register(a); second.Register(b);
                first.Advance(1);
                Assert.That(a.Calls, Is.EqualTo(1)); Assert.That(b.Calls, Is.Zero);
                first.Dispose(); first.Time.Advance(1); second.Advance(1);
                Assert.That(a.Calls, Is.EqualTo(1)); Assert.That(b.Calls, Is.EqualTo(1));
            }
        }
    }
}
