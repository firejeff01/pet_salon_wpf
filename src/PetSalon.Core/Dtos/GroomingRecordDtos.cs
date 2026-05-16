using PetSalon.Core.Entities;

namespace PetSalon.Core.Dtos;

public sealed class GroomingServiceInput
{
    public string Item { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed class GroomingRecordInput
{
    public string AppointmentId { get; set; } = string.Empty;
    public List<GroomingServiceInput> Services { get; set; } = new();
    public PhysicalExamination PhysicalExamination { get; set; } = new();
    public List<string> Personality { get; set; } = new();
    public List<string> MedicalHistory { get; set; } = new();
    public string MedicalHistoryOther { get; set; } = string.Empty;
    public string OwnerNotes { get; set; } = string.Empty;
    public string ShopNotes { get; set; } = string.Empty;
    public string OtherNotes { get; set; } = string.Empty;
}

public sealed record StoredValueCalc(decimal Deduction, decimal Cash, decimal Remaining);
