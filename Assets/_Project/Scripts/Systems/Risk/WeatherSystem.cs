using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Risk;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Risk
{
    public sealed class RegionalWeatherState
    {public string Region {get;internal set;}public WeatherKind Weather {get;internal set;}public double Volatility {get;internal set;}}
    public sealed class WeatherSystem : IRegisterableSystem
    {
        private readonly SortedDictionary<string,RegionalWeatherState> regions=new SortedDictionary<string,RegionalWeatherState>(StringComparer.Ordinal);
        private readonly SeededRandom random;private readonly RiskSettings settings;
        public WeatherSystem(IEnumerable<string> regions,SeededRandom random,RiskSettings settings)
        {this.random=random??throw new ArgumentNullException(nameof(random));this.settings=settings??throw new ArgumentNullException(nameof(settings));settings.Validate();foreach(var id in regions.Distinct())this.regions.Add(id,new RegionalWeatherState {Region=id,Weather=WeatherKind.Clear,Volatility=.2});}
        public void EnsureRegion(string region){if(!regions.ContainsKey(region))regions.Add(region,new RegionalWeatherState {Region=region,Weather=WeatherKind.Clear,Volatility=.2});}
        public Func<long,double> SeasonalFrequency {get;set;}
        public RegionalWeatherState Get(string region)=>regions[region];
        public double Modifier(string region,string phaseId)
        {double exposure=settings.IndoorPhaseIds.Contains(phaseId)?settings.IndoorWeatherExposure:1;return 1-(1-settings.WeatherMultipliers[(int)Get(region).Weather])*exposure;}
        public void ForceShift(string region,WeatherKind weather)
        {if(!Enum.IsDefined(typeof(WeatherKind),weather))throw new ArgumentException("Unknown weather.");var state=Get(region);state.Weather=weather;GameEvents.Publish(new WeatherChangedEvent(region,weather));}
        public void OnTick(TickContext context)
        {
            foreach(var state in regions.Values)
            {
                if(random.NextDouble()>=1-Math.Exp(-settings.WeatherShiftRate*(SeasonalFrequency?.Invoke(context.TotalHours)??1)*(1+state.Volatility)*context.ElapsedGameHours))continue;
                state.Volatility=Math.Max(0,Math.Min(1,state.Volatility+(random.NextDouble()*2-1)*settings.WeatherDriftStep));
                int direction=random.Next(2)==0?-1:1;int next=((int)state.Weather+direction+7)%7;
                ForceShift(state.Region,(WeatherKind)next);
            }
        }
    }
}
