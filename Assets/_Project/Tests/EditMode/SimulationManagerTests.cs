using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Simulation;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public class SimulationManagerTests
    {
        private sealed class TestSystem : IRegisterableSystem
        {
            private readonly Action<TickContext> callback;
            public TestSystem(Action<TickContext> callback) { this.callback = callback; }
            public void OnTick(TickContext context) => callback(context);
        }

        [Test]
        public void DriverTicksOncePerHour_InRegistrationOrder_WithoutDuplicates()
        {
            using (var simulation = new SimulationManager(new CompanyData("Test", 100f), new GameTimeData(), 42))
            {
                var calls = new List<string>();
                var first = new TestSystem(tick => calls.Add("A" + tick.TotalHours));
                var second = new TestSystem(tick => calls.Add("B" + tick.TotalHours));
                simulation.Register(first);
                simulation.Register(first);
                simulation.Register(second);
                simulation.Advance(2d);
                Assert.That(calls, Is.EqualTo(new[] { "A1", "B1", "A2", "B2" }));
                Assert.That(simulation.Unregister(first), Is.True);
                simulation.Advance(1d);
                Assert.That(calls[calls.Count - 1], Is.EqualTo("B3"));
            }
        }

        [Test]
        public void RegistrationChanges_DuringDispatchApplyOnNextTick()
        {
            using (var simulation = new SimulationManager(new CompanyData("Test", 100f), new GameTimeData(), 42))
            {
                var calls = new List<string>();
                var second = new TestSystem(tick => calls.Add("B" + tick.TotalHours));
                var third = new TestSystem(tick => calls.Add("C" + tick.TotalHours));
                simulation.Register(new TestSystem(tick =>
                {
                    calls.Add("A" + tick.TotalHours);
                    simulation.Unregister(second);
                    simulation.Register(third);
                }));
                simulation.Register(second);
                simulation.Advance(2d);
                Assert.That(calls, Is.EqualTo(new[] { "A1", "B1", "A2", "C2" }));
            }
        }
    }
}
