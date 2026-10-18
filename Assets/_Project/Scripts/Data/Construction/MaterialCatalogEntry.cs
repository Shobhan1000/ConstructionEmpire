using System;
namespace ConstructionEmpire.Data.Construction
{
    [Serializable]
    public sealed class MaterialCatalogEntry
    {
        public string Id, Name, Unit, Supplier, StorageRequirement;
        public double Price, Availability, Quality;
        public int LeadHours;
        public void Validate()
        {
            ConstructionValidation.Text(Id,nameof(Id)); ConstructionValidation.Text(Name,nameof(Name));
            ConstructionValidation.Text(Unit,nameof(Unit)); ConstructionValidation.Text(Supplier,nameof(Supplier));
            ConstructionValidation.Positive(Price,nameof(Price)); ConstructionValidation.NonNegative(Availability,nameof(Availability));
            ConstructionValidation.Unit(Quality,nameof(Quality));
            if(LeadHours<1) throw new ArgumentOutOfRangeException(nameof(LeadHours));
        }
        public MaterialCatalogEntry Copy() => (MaterialCatalogEntry)MemberwiseClone();
    }
}
