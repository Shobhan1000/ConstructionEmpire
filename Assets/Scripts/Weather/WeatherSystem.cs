using ConstructionEmpire.Core;
using ConstructionEmpire.Data;
using ConstructionEmpire.Utilities;

namespace ConstructionEmpire.Weather
{
    public readonly struct WeatherChangedEvent : IGameEvent
    {
        public readonly WeatherType NewWeather;
        public WeatherChangedEvent(WeatherType newWeather) { NewWeather = newWeather; }
    }

    /// <summary>
    /// A simple daily weather roll affecting outdoor construction phases
    /// (spec section 23). Rerolled independently each day rather than
    /// persisting in multi-day fronts - a reasonable simplification for
    /// now; a "this weather system lasts N days" model would be the
    /// natural next step if single-day swings feel too noisy in practice.
    /// </summary>
    public class WeatherSystem
    {
        private readonly SeededRandom _random;

        private static readonly (WeatherType Type, float Weight, float ProgressModifier)[] WeatherTable =
        {
            (WeatherType.Clear, 50f, 1.0f),
            (WeatherType.Wind, 15f, 0.85f),
            (WeatherType.Rain, 15f, 0.7f),
            (WeatherType.ExtremeHeat, 8f, 0.8f),
            (WeatherType.HeavyRain, 6f, 0.4f),
            (WeatherType.Snow, 4f, 0.5f),
            (WeatherType.Storm, 2f, 0.2f)
        };

        public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;

        public WeatherSystem(SeededRandom random)
        {
            _random = random;
            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
        }

        /// <summary>1.0 for indoor work regardless of weather; otherwise today's weather modifier.</summary>
        public float GetProgressModifier(bool isOutdoorWork)
        {
            if (!isOutdoorWork) return 1f;

            foreach (var entry in WeatherTable)
            {
                if (entry.Type == CurrentWeather) return entry.ProgressModifier;
            }

            return 1f;
        }

        private void OnDayAdvanced(DayAdvancedEvent evt)
        {
            float totalWeight = 0f;
            foreach (var entry in WeatherTable) totalWeight += entry.Weight;

            float roll = _random.NextFloat(0f, totalWeight);
            float cumulative = 0f;
            WeatherType chosen = WeatherType.Clear;

            foreach (var entry in WeatherTable)
            {
                cumulative += entry.Weight;
                if (roll <= cumulative)
                {
                    chosen = entry.Type;
                    break;
                }
            }

            if (chosen != CurrentWeather)
            {
                CurrentWeather = chosen;
                EventBus.Publish(new WeatherChangedEvent(chosen));
            }
        }
    }
}
