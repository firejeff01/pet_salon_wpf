using PetSalon.Core.Entities;

namespace PetSalon.Core.Dtos;

public sealed class CustomerRegistrationInput
{
    public OwnerInput Owner { get; set; } = new();
    public List<CustomerPetInput> Pets { get; set; } = new();
    public bool AllowDuplicate { get; set; }
}

public sealed class CustomerPetInput
{
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Age { get; set; } = string.Empty;
    public bool IsNeutered { get; set; }
    public string ChipStatus { get; set; } = Constants.ChipStatusOptions.Unspecified;
    public string? ChipData { get; set; }
    public List<string> Personality { get; set; } = new();
    public List<string> MedicalHistory { get; set; } = new();
    public string MedicalHistoryOther { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public sealed record PotentialDuplicateOwner(
    string OwnerId,
    string Name,
    string NationalId,
    string Phone);

public sealed record DuplicateOwnerCheckResult(IReadOnlyList<PotentialDuplicateOwner> Matches)
{
    public bool HasPotentialDuplicate => Matches.Count > 0;
}

public sealed record CustomerRegistrationResult(Owner Owner, IReadOnlyList<Pet> Pets);
