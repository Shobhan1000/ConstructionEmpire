namespace ConstructionEmpire.Systems.Simulation.Events
{
    public readonly struct HourlyTickEvent
    {
        public GameTimeManager Source { get; }
        public TickContext Context { get; }
        public HourlyTickEvent(GameTimeManager source, TickContext context)
        {
            Source = source;
            Context = context;
        }
    }
}
