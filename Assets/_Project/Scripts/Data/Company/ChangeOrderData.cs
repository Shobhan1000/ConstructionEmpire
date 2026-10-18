using System;
namespace ConstructionEmpire.Data.Company
{
    public enum ChangeOrderStatus { Proposed,Accepted,Rejected }
    [Serializable]public sealed class ChangeOrderData
    {public string Id,ProjectId,PhaseId,Description;public double ScopeFraction,EstimatedCost,OfferedPrice;public long ExtensionHours;public bool Negotiated;public ChangeOrderStatus Status;}
}
