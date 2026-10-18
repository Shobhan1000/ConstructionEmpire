using System;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Simulation.Events;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Simulation;
using NUnit.Framework;

namespace ConstructionEmpire.Tests.EditMode
{
    public class GameTimeManagerTests
    {
        [SetUp] public void SetUp() => GameEvents.Clear();
        [TearDown] public void TearDown() => GameEvents.Clear();
        private static GameTimeManager Create(SimulationSpeed speed, int cap = 10000) =>
            new GameTimeManager(new GameTimeData { Speed = speed, MaxTicksPerCall = cap });

        [TestCase(SimulationSpeed.Pause)]
        [TestCase(SimulationSpeed.Normal)]
        [TestCase(SimulationSpeed.Double)]
        [TestCase(SimulationSpeed.Fast)]
        [TestCase(SimulationSpeed.Faster)]
        [TestCase(SimulationSpeed.Maximum)]
        public void DateAndBoundaryEvents_AreAccurateAtEverySpeed(SimulationSpeed speed)
        {
            var clock = Create(speed);
            int hours = 0, days = 0, weeks = 0, months = 0;
            GameEvents.Subscribe<HourlyTickEvent>(payload => { hours++; Assert.That(payload.Context.ElapsedGameHours, Is.EqualTo(1)); });
            GameEvents.Subscribe<DailyTickEvent>(payload => days++);
            GameEvents.Subscribe<WeeklyTickEvent>(payload => weeks++);
            GameEvents.Subscribe<MonthlyTickEvent>(payload => months++);
            clock.Advance(speed == SimulationSpeed.Pause ? 672d : 672d / (int)speed);
            bool paused = speed == SimulationSpeed.Pause;
            Assert.That(clock.TotalHours, Is.EqualTo(paused ? 0 : 672));
            Assert.That(clock.Hour, Is.Zero);
            Assert.That(clock.Day, Is.EqualTo(paused ? 1 : 29));
            Assert.That(clock.Week, Is.EqualTo(paused ? 1 : 5));
            Assert.That(clock.Month, Is.EqualTo(paused ? 1 : 2));
            Assert.That(hours, Is.EqualTo(paused ? 0 : 672));
            Assert.That(days, Is.EqualTo(paused ? 0 : 28));
            Assert.That(weeks, Is.EqualTo(paused ? 0 : 4));
            Assert.That(months, Is.EqualTo(paused ? 0 : 1));
        }

        [TestCase(SimulationSpeed.Normal)]
        [TestCase(SimulationSpeed.Double)]
        [TestCase(SimulationSpeed.Fast)]
        [TestCase(SimulationSpeed.Faster)]
        [TestCase(SimulationSpeed.Maximum)]
        public void FramePartitioning_DoesNotChangeSimulationTime(SimulationSpeed speed)
        {
            var whole = Create(speed);
            var fragmented = Create(speed);
            whole.Advance(100d);
            for (int i = 0; i < 1000; i++) fragmented.Advance(0.1d);
            Assert.That(fragmented.TotalHours, Is.EqualTo(whole.TotalHours));
            Assert.That(fragmented.PendingSimulationSeconds,
                Is.EqualTo(whole.PendingSimulationSeconds).Within(1e-9));
        }

        [Test]
        public void CapRetainsDebt_AndPauseFreezesIt()
        {
            var clock = Create(SimulationSpeed.Normal, 5);
            Assert.That(clock.Advance(20d), Is.EqualTo(5));
            Assert.That(clock.PendingSimulationSeconds, Is.EqualTo(15d));
            clock.Speed = SimulationSpeed.Pause;
            Assert.That(clock.Advance(1000d), Is.Zero);
            Assert.That(clock.PendingSimulationSeconds, Is.EqualTo(15d));
            clock.Speed = SimulationSpeed.Normal;
            Assert.That(clock.Advance(0d), Is.EqualTo(5));
            Assert.That(clock.TotalHours, Is.EqualTo(10));
            Assert.That(clock.PendingSimulationSeconds, Is.EqualTo(10d));
        }

        [Test]
        public void SpeedChangesAndPause_PreserveFractionalWork()
        {
            var clock = Create(SimulationSpeed.Normal);
            clock.Advance(0.5d);
            clock.Speed = SimulationSpeed.Pause;
            clock.Advance(100d);
            clock.Speed = SimulationSpeed.Double;
            Assert.That(clock.Advance(0.25d), Is.EqualTo(1));
            Assert.That(clock.TotalHours, Is.EqualTo(1));
        }

        [Test]
        public void CustomCalendarAndTickLength_RespectBoundaries()
        {
            var clock = new GameTimeManager(new GameTimeData
                { SecondsPerTick = 2d, HoursPerDay = 10, DaysPerWeek = 3, DaysPerMonth = 9 });
            clock.Advance(180d);
            Assert.That(clock.TotalHours, Is.EqualTo(90));
            Assert.That(clock.Day, Is.EqualTo(10));
            Assert.That(clock.Week, Is.EqualTo(4));
            Assert.That(clock.Month, Is.EqualTo(2));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidElapsedTime_IsRejected(double elapsed)
        {
            var clock = Create(SimulationSpeed.Normal);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(elapsed));
            Assert.That(clock.TotalHours, Is.Zero);
        }

        [Test]
        public void InvalidSpeed_IsRejected() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(SimulationSpeed.Normal).Speed = (SimulationSpeed)3);

        [Test]
        public void RecursiveAdvance_IsRejected()
        {
            var clock = Create(SimulationSpeed.Normal);
            GameEvents.Subscribe<HourlyTickEvent>(payload =>
                Assert.Throws<InvalidOperationException>(() => clock.Advance(1d)));
            Assert.That(clock.Advance(1d), Is.EqualTo(1));
        }
    }
}
