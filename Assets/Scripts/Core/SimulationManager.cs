using System;
using System.Collections.Generic;

namespace ConstructionEmpire.Core
{
    /// <summary>
    /// Holds one-off scheduled events (e.g. "material arrives in 3 days",
    /// added in later milestones) and fires them as game time passes.
    /// Deliberately has no Update() of its own - it reacts to
    /// HourAdvancedEvent from GameTimeManager, so the whole simulation
    /// scales on one clock instead of many (spec section 53/61).
    /// </summary>
    public class SimulationManager
    {
        private class ScheduledEvent
        {
            public string Id;
            public DateTime ExecuteAt;
            public Action Callback;
        }

        private readonly List<ScheduledEvent> _scheduledEvents = new List<ScheduledEvent>();

        public SimulationManager()
        {
            EventBus.Subscribe<HourAdvancedEvent>(OnHourAdvanced);
        }

        /// <summary>Schedules callback to fire the first time the clock reaches or passes executeAt. Returns an id you can use to cancel it.</summary>
        public string ScheduleEvent(DateTime executeAt, Action callback, string id = null)
        {
            if (id == null)
            {
                id = Guid.NewGuid().ToString("N");
            }

            _scheduledEvents.Add(new ScheduledEvent { Id = id, ExecuteAt = executeAt, Callback = callback });
            return id;
        }

        public bool CancelScheduledEvent(string id)
        {
            return _scheduledEvents.RemoveAll(e => e.Id == id) > 0;
        }

        public int PendingEventCount => _scheduledEvents.Count;

        private void OnHourAdvanced(HourAdvancedEvent evt)
        {
            for (int i = _scheduledEvents.Count - 1; i >= 0; i--)
            {
                var scheduled = _scheduledEvents[i];
                if (scheduled.ExecuteAt <= evt.Date)
                {
                    _scheduledEvents.RemoveAt(i);
                    scheduled.Callback?.Invoke();
                }
            }
        }

        /// <summary>Unsubscribes from the EventBus. Call when tearing down (e.g. GameBootstrap.OnDestroy) to avoid leaking a subscription onto a destroyed simulation.</summary>
        public void Dispose()
        {
            EventBus.Unsubscribe<HourAdvancedEvent>(OnHourAdvanced);
        }
    }
}
