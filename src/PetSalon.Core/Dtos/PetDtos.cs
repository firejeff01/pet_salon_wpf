using PetSalon.Core.Entities;

namespace PetSalon.Core.Dtos;

public sealed class PetInput
{
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Age { get; set; } = string.Empty;
    public bool IsNeutered { get; set; }
    public string? ChipNumber { get; set; }
    public string? UnregisteredIdMethod { get; set; }
    public List<string> Personality { get; set; } = new();
    public PhysicalExamination PhysicalExamination { get; set; } = new();
    public List<string> MedicalHistory { get; set; } = new();
    public string MedicalHistoryOther { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
