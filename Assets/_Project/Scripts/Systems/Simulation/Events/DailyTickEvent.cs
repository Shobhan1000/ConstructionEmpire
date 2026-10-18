namespace ConstructionEmpire.Systems.Simulation.Events
{
    public readonly struct DailyTickEvent
    {
        public GameTimeManager Source { get; }
        public TickContext Context { get; }
        public DailyTickEvent(GameTimeManager source, TickContext context)
        {
            Source = source;
            Context = context;
        }
    }
}
