using ConstructionEmpire.Data.Company;
namespace ConstructionEmpire.Data.Events
{
    public readonly struct CompanyIdentityChangedEvent
    { public CompanyData Company { get; } public CompanyIdentityChangedEvent(CompanyData company) { Company=company; } }
    public readonly struct ResourcesChangedEvent
    { public string ProjectId { get; } public ResourcesChangedEvent(string projectId) { ProjectId=projectId; } }
    public readonly struct MaterialPricesChangedEvent { }
    public readonly struct NotificationAddedEvent
    { public string Id { get; } public NotificationAddedEvent(string id) { Id=id; } }
    public readonly struct NavigationRequestedEvent
    {
        public string Screen { get; } public string EntityId { get; }
        public NavigationRequestedEvent(string screen,string entityId=null) { Screen=screen; EntityId=entityId; }
    }
}
