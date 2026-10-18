using System;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Simulation.Events;
using ConstructionEmpire.Data.Time;

namespace ConstructionEmpire.Systems.Simulation
{
    public sealed class GameTimeManager
    {
        private readonly GameTimeData state;
        private bool advancing;

        public GameTimeManager(GameTimeData initialState)
        {
            if (initialState == null) throw new ArgumentNullException(nameof(initialState));
            initialState.Validate();
            state = initialState.Copy();
        }

        public TickContext Current => new TickContext(state.TotalHours, state.HoursPerDay,
            state.DaysPerWeek, state.DaysPerMonth);
        public int Hour => Current.Hour;
        public long Day => Current.Day;
        public long Week => Current.Week;
        public long Month => Current.Month;
        public long TotalHours => state.TotalHours;
        public double PendingSimulationSeconds => state.PendingSimulationSeconds;
        public SimulationSpeed Speed
        {
            get => state.Speed;
            set
            {
                if (!Enum.IsDefined(typeof(SimulationSpeed), value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                state.Speed = value;
            }
        }
        public GameTimeData CaptureState() => state.Copy();
        internal void RestorePlaybackState(GameTimeData saved)
        {
            saved.Validate();
            if(advancing || saved.TotalHours!=state.TotalHours || saved.SecondsPerTick!=state.SecondsPerTick || saved.HoursPerDay!=state.HoursPerDay || saved.DaysPerWeek!=state.DaysPerWeek || saved.DaysPerMonth!=state.DaysPerMonth)throw new InvalidOperationException("Restored clock does not match the reconstructed simulation.");
            state.PendingSimulationSeconds=saved.PendingSimulationSeconds;state.Speed=SimulationSpeed.Pause;
        }

        /// <summary>
        /// Accrue scaled work, then drain fixed one-hour ticks. Never vary tick length.
        /// The cap retains debt for later calls. Pausing freezes both debt and accrual.
        /// Host elapsed time is only scheduling input, never a systems/economics delta.
        /// </summary>
        public int Advance(double elapsedRealSeconds)
        {
            if (double.IsNaN(elapsedRealSeconds) || double.IsInfinity(elapsedRealSeconds) || elapsedRealSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedRealSeconds));
            if (advancing) throw new InvalidOperationException("Clock advancement cannot be re-entered.");
            if (Speed == SimulationSpeed.Pause) return 0;

            double accumulated = state.PendingSimulationSeconds + elapsedRealSeconds * (int)Speed;
            if (double.IsInfinity(accumulated))
                throw new OverflowException("Elapsed time exceeds accumulator capacity.");
            state.PendingSimulationSeconds = accumulated;

            int processed = 0;
            advancing = true;
            try
            {
                // Tolerance handles floating-point roundoff when splitting elapsed time across frames.
                double tolerance = state.SecondsPerTick * 1e-12;
                while (processed < state.MaxTicksPerCall && Speed != SimulationSpeed.Pause &&
                       state.PendingSimulationSeconds + tolerance >= state.SecondsPerTick)
                {
                    if (state.TotalHours == long.MaxValue)
                        throw new OverflowException("Clock reached its maximum supported hour.");
                    state.PendingSimulationSeconds = Math.Max(0d,
                        state.PendingSimulationSeconds - state.SecondsPerTick);
                    state.TotalHours++;
                    processed++;
                    TickContext context = Current;
                    GameEvents.Publish(new HourlyTickEvent(this, context));
                    if (context.StartsDay) GameEvents.Publish(new DailyTickEvent(this, context));
                    if (context.StartsWeek) GameEvents.Publish(new WeeklyTickEvent(this, context));
                    if (context.StartsMonth) GameEvents.Publish(new MonthlyTickEvent(this, context));
                }
                return processed;
            }
            finally { advancing = false; }
        }
    }
}
