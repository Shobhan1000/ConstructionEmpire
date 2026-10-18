using System;
using UnityEngine;

namespace ConstructionEmpire.Core
{
    /// <summary>
    /// Central game clock (spec section 53/55). Converts real time into
    /// game time and publishes Hour/Day/Week/Month events via the
    /// EventBus. No other system should run its own Update() loop for
    /// time-based logic - subscribe to these events instead (see
    /// SimulationManager for scheduling future one-off events).
    /// </summary>
    public class GameTimeManager : MonoBehaviour
    {
        private float _realSecondsPerGameHour = 3f;
        private float _hourAccumulator;
        private bool _initialized;
        private TimeScale _scaleBeforePause = TimeScale.Normal;

        public DateTime CurrentDate { get; private set; }
        public TimeScale CurrentTimeScale { get; private set; } = TimeScale.Normal;
        public bool IsPaused => CurrentTimeScale == TimeScale.Paused;

        public void Initialize(float realSecondsPerGameHour, DateTime startDate)
        {
            _realSecondsPerGameHour = Mathf.Max(0.01f, realSecondsPerGameHour);
            CurrentDate = startDate;
            _hourAccumulator = 0f;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || IsPaused) return;

            float multiplier = (float)CurrentTimeScale;
            _hourAccumulator += Time.deltaTime * multiplier;

            // Safety cap: if realSecondsPerGameHour is ever misconfigured to
            // ~0, this stops the game from hard-locking in an infinite loop.
            int safety = 0;
            while (_hourAccumulator >= _realSecondsPerGameHour && safety < 1000)
            {
                _hourAccumulator -= _realSecondsPerGameHour;
                AdvanceOneHour();
                safety++;
            }
        }

        /// <summary>Advances the clock by exactly one game hour and fires the relevant tick events. Safe to call directly from tests or debug tools.</summary>
        public void AdvanceOneHour()
        {
            CurrentDate = CurrentDate.AddHours(1);
            EventBus.Publish(new HourAdvancedEvent(CurrentDate));

            if (CurrentDate.Hour == 0)
            {
                EventBus.Publish(new DayAdvancedEvent(CurrentDate));

                if (CurrentDate.DayOfWeek == DayOfWeek.Monday)
                {
                    EventBus.Publish(new WeekAdvancedEvent(CurrentDate));
                }

                if (CurrentDate.Day == 1)
                {
                    EventBus.Publish(new MonthAdvancedEvent(CurrentDate));
                }
            }
        }

        /// <summary>Debug helper: advances a full day (24 hours) immediately. Wire this up to the debug menu in a later milestone (spec section 59).</summary>
        public void AdvanceOneDayImmediate()
        {
            for (int i = 0; i < 24; i++)
            {
                AdvanceOneHour();
            }
        }

        public void SetTimeScale(TimeScale newScale)
        {
            if (CurrentTimeScale == newScale) return;
            CurrentTimeScale = newScale;
            EventBus.Publish(new TimeScaleChangedEvent(newScale));
        }

        public void Pause()
        {
            if (IsPaused) return;
            _scaleBeforePause = CurrentTimeScale;
            SetTimeScale(TimeScale.Paused);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            SetTimeScale(_scaleBeforePause);
        }
    }
}
