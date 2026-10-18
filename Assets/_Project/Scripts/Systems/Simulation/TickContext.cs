namespace ConstructionEmpire.Systems.Simulation
{
    public readonly struct TickContext
    {
        public long TotalHours { get; }
        public int Hour { get; }
        public long Day { get; }
        public long Week { get; }
        public long Month { get; }
        public int ElapsedGameHours => 1;
        public bool StartsDay { get; }
        public bool StartsWeek { get; }
        public bool StartsMonth { get; }

        public TickContext(long totalHours, int hoursPerDay, int daysPerWeek, int daysPerMonth)
        {
            TotalHours = totalHours;
            long completedDays = totalHours / hoursPerDay;
            Hour = (int)(totalHours % hoursPerDay);
            Day = completedDays + 1;
            Week = completedDays / daysPerWeek + 1;
            Month = completedDays / daysPerMonth + 1;
            StartsDay = totalHours > 0 && Hour == 0;
            StartsWeek = StartsDay && completedDays % daysPerWeek == 0;
            StartsMonth = StartsDay && completedDays % daysPerMonth == 0;
        }
    }
}
