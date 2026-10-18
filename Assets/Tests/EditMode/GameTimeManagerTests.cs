using System;
using NUnit.Framework;
using UnityEngine;
using ConstructionEmpire.Core;

namespace ConstructionEmpire.Tests
{
    public class GameTimeManagerTests
    {
        private GameObject _go;
        private GameTimeManager _timeManager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestGameTimeManager");
            _timeManager = _go.AddComponent<GameTimeManager>();
            _timeManager.Initialize(3f, new DateTime(2025, 1, 1, 0, 0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
            EventBus.Clear();
        }

        [Test]
        public void AdvanceOneHour_IncrementsDateByOneHour()
        {
            var before = _timeManager.CurrentDate;
            _timeManager.AdvanceOneHour();
            Assert.AreEqual(before.AddHours(1), _timeManager.CurrentDate);
        }

        [Test]
        public void AdvanceOneHour_AtMidnight_PublishesDayAdvancedEvent()
        {
            bool dayAdvancedFired = false;
            EventBus.Subscribe<DayAdvancedEvent>(_ => dayAdvancedFired = true);

            _timeManager.Initialize(3f, new DateTime(2025, 1, 1, 23, 0, 0));
            _timeManager.AdvanceOneHour();

            Assert.IsTrue(dayAdvancedFired);
        }

        [Test]
        public void AdvanceOneDayImmediate_Advances24Hours()
        {
            var before = _timeManager.CurrentDate;
            _timeManager.AdvanceOneDayImmediate();
            Assert.AreEqual(before.AddDays(1), _timeManager.CurrentDate);
        }

        [Test]
        public void Pause_SetsIsPausedTrue()
        {
            _timeManager.Pause();
            Assert.IsTrue(_timeManager.IsPaused);
        }

        [Test]
        public void Resume_RestoresPreviousTimeScale()
        {
            _timeManager.SetTimeScale(TimeScale.Fast5x);
            _timeManager.Pause();
            _timeManager.Resume();
            Assert.AreEqual(TimeScale.Fast5x, _timeManager.CurrentTimeScale);
        }
    }
}
