using System;

namespace ConstructionEmpire.Core
{
    /// <summary>Marker interface for anything published on the EventBus.</summary>
    public interface IGameEvent { }

    public enum TimeScale
    {
        Paused = 0,
        Normal = 1,
        Fast2x = 2,
        Fast5x = 5,
        Fast10x = 10,
        Fast25x = 25
    }

    public readonly struct HourAdvancedEvent : IGameEvent
    {
        public readonly DateTime Date;
        public HourAdvancedEvent(DateTime date) { Date = date; }
    }

    public readonly struct DayAdvancedEvent : IGameEvent
    {
        public readonly DateTime Date;
        public DayAdvancedEvent(DateTime date) { Date = date; }
    }

    public readonly struct WeekAdvancedEvent : IGameEvent
    {
        public readonly DateTime Date;
        public WeekAdvancedEvent(DateTime date) { Date = date; }
    }

    public readonly struct MonthAdvancedEvent : IGameEvent
    {
        public readonly DateTime Date;
        public MonthAdvancedEvent(DateTime date) { Date = date; }
    }

    public readonly struct TimeScaleChangedEvent : IGameEvent
    {
        public readonly TimeScale NewScale;
        public TimeScaleChangedEvent(TimeScale newScale) { NewScale = newScale; }
    }

    public readonly struct CompanyFoundedEvent : IGameEvent
    {
        public readonly string CompanyName;
        public readonly double StartingCash;
        public CompanyFoundedEvent(string companyName, double startingCash)
        {
            CompanyName = companyName;
            StartingCash = startingCash;
        }
    }

    public readonly struct CompanyCashChangedEvent : IGameEvent
    {
        public readonly double NewCash;
        public readonly double Delta;
        public readonly string Reason;
        public CompanyCashChangedEvent(double newCash, double delta, string reason)
        {
            NewCash = newCash;
            Delta = delta;
            Reason = reason;
        }
    }

    public readonly struct CompanyReputationChangedEvent : IGameEvent
    {
        public readonly float NewReputation;
        public readonly float Delta;
        public readonly string Reason;
        public CompanyReputationChangedEvent(float newReputation, float delta, string reason)
        {
            NewReputation = newReputation;
            Delta = delta;
            Reason = reason;
        }
    }

    public readonly struct GameSavedEvent : IGameEvent
    {
        public readonly string SlotName;
        public GameSavedEvent(string slotName) { SlotName = slotName; }
    }

    public readonly struct GameLoadedEvent : IGameEvent
    {
        public readonly string SlotName;
        public GameLoadedEvent(string slotName) { SlotName = slotName; }
    }
}
