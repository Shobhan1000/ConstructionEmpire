namespace ConstructionEmpire.Systems.Simulation
{
    public interface IRegisterableSystem
    {
        void OnTick(TickContext context);
    }
}
