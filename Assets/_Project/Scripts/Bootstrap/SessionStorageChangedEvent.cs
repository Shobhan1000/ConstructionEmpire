namespace ConstructionEmpire.Bootstrap
{
    public readonly struct SessionStorageChangedEvent
    {
        public readonly SimulationBootstrapper Source;public readonly string Message;public readonly bool Loading;
        public SessionStorageChangedEvent(SimulationBootstrapper source,string message,bool loading){Source=source;Message=message;Loading=loading;}
    }
}
