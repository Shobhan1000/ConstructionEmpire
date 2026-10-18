using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Systems.Construction
{
    /// <summary>Supplies the existing site/weather slots and inspection completion gate, without changing the six-factor formula.</summary>
    public interface IPhaseConditions
    {
        double SiteModifier(ProjectData project,ConstructionPhaseRuntimeData phase,long hour);
        double WeatherModifier(ProjectData project,ConstructionPhaseRuntimeData phase);
        bool CanComplete(ProjectData project,ConstructionPhaseRuntimeData phase,long hour);
    }
}
