using System;
using ConstructionEmpire.Data.Market;
namespace ConstructionEmpire.Systems.Market
{
    public sealed class SeasonalitySystem
    {
        private readonly MarketSettings settings;private readonly long hoursPerMonth;
        public SeasonalitySystem(MarketSettings settings,int hoursPerDay,int daysPerMonth){this.settings=settings??throw new ArgumentNullException(nameof(settings));settings.Validate();if(hoursPerDay<1 || daysPerMonth<1)throw new ArgumentOutOfRangeException();hoursPerMonth=checked((long)hoursPerDay*daysPerMonth);}
        public Season At(long hour){if(hour<0)throw new ArgumentOutOfRangeException(nameof(hour));return (Season)((hour/hoursPerMonth/settings.MonthsPerSeason)%4);}
        public double DemandMultiplier(long hour)=>settings.SeasonalDemand[(int)At(hour)];
        public double WeatherFrequency(long hour)=>settings.SeasonalWeatherFrequency[(int)At(hour)];
    }
}
