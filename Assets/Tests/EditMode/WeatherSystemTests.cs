using ConstructionEmpire.Data;
using System;
using NUnit.Framework;
using ConstructionEmpire.Core;
using ConstructionEmpire.Utilities;
using ConstructionEmpire.Weather;

namespace ConstructionEmpire.Tests
{
    public class WeatherSystemTests
    {
        private WeatherSystem _weather;

        [SetUp]
        public void SetUp()
        {
            _weather = new WeatherSystem(new SeededRandom(21));
        }

        [TearDown]
        public void TearDown()
        {
            _weather.Dispose();
            EventBus.Clear();
        }

        [Test]
        public void GetProgressModifier_ForIndoorWork_IsAlwaysOne()
        {
            for (int i = 0; i < 30; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(new DateTime(2025, 1, 1).AddDays(i)));
                Assert.AreEqual(1f, _weather.GetProgressModifier(isOutdoorWork: false));
            }
        }

        [Test]
        public void GetProgressModifier_ForOutdoorWork_StaysWithinValidRange()
        {
            for (int i = 0; i < 30; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(new DateTime(2025, 1, 1).AddDays(i)));
                float modifier = _weather.GetProgressModifier(isOutdoorWork: true);

                Assert.GreaterOrEqual(modifier, 0.2f);
                Assert.LessOrEqual(modifier, 1.0f);
            }
        }

        [Test]
        public void CurrentWeather_ChangesOverManyDays()
        {
            bool sawNonClearWeather = false;

            for (int i = 0; i < 60; i++)
            {
                EventBus.Publish(new DayAdvancedEvent(new DateTime(2025, 1, 1).AddDays(i)));
                if (_weather.CurrentWeather != WeatherType.Clear)
                {
                    sawNonClearWeather = true;
                    break;
                }
            }

            Assert.IsTrue(sawNonClearWeather, "Expected at least one non-Clear day across 60 rolls - if this fails consistently, check the weather weighting table.");
        }
    }
}
