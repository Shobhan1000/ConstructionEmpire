namespace ConstructionEmpire.Systems.Simulation.Events
{
    public readonly struct WeeklyTickEvent
    {
        public GameTimeManager Source { get; }
        public TickContext Context { get; }
        public WeeklyTickEvent(GameTimeManager source, TickContext context)
        {
            Source = source;
            Context = context;
        }
    }
}
