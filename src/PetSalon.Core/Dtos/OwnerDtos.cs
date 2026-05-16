namespace PetSalon.Core.Dtos;

public sealed class OwnerInput
{
    public string Name { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;
    public string EmergencyContactRelationship { get; set; } = string.Empty;
    public string PreferredAnimalHospitalName { get; set; } = string.Empty;
    public string PreferredAnimalHospitalPhone { get; set; } = string.Empty;
    public string PreferredAnimalHospitalAddress { get; set; } = string.Empty;
    public bool IsStoredValueCustomer { get; set; }
    public decimal StoredValueBalance { get; set; }
    public string Note { get; set; } = string.Empty;
}
