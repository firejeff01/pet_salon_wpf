using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Constants;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;

namespace PetSalon.Core.Services;

public sealed class CustomerRegistrationService
{
    private readonly IPetSalonDbContext _db;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public CustomerRegistrationService(IPetSalonDbContext db, IIdGenerator ids, IClock clock)
    {
        _db = db;
        _ids = ids;
        _clock = clock;
    }

    public async Task<DuplicateOwnerCheckResult> CheckDuplicateAsync(
        OwnerInput input,
        CancellationToken ct = default)
    {
        var nationalId = NormalizeNationalId(input.NationalId);
        var phone = NormalizePhone(input.Phone);
        var candidates = await _db.Owners.AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new PotentialDuplicateOwner(o.OwnerId, o.Name, o.NationalId, o.Phone))
            .ToListAsync(ct);
        var matches = candidates
            .Where(o => NormalizeNationalId(o.NationalId) == nationalId || NormalizePhone(o.Phone) == phone)
            .ToList();
        return new DuplicateOwnerCheckResult(matches);
    }

    public async Task<CustomerRegistrationResult> CreateAsync(
        CustomerRegistrationInput input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        OwnerService.Validate(input.Owner);

        var petInputs = input.Pets
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(ToPetInput)
            .ToList();
        foreach (var petInput in petInputs)
            PetService.Validate(petInput);

        if (!input.AllowDuplicate)
        {
            var duplicate = await CheckDuplicateAsync(input.Owner, ct);
            if (duplicate.HasPotentialDuplicate)
                throw AppException.Conflict(
                    "POTENTIAL_DUPLICATE_OWNER",
                    "身分證字號或電話與既有顧客相同",
                    duplicate.Matches);
        }

        var now = _clock.Now;
        var owner = new Owner
        {
            OwnerId = _ids.New("owner"),
            CreatedAt = now,
            UpdatedAt = now,
        };
        OwnerService.Apply(owner, input.Owner);
        _db.Owners.Add(owner);

        var pets = new List<Pet>(petInputs.Count);
        foreach (var petInput in petInputs)
        {
            petInput.OwnerId = owner.OwnerId;
            var pet = new Pet
            {
                PetId = _ids.New("pet"),
                OwnerId = owner.OwnerId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            PetService.Apply(pet, petInput);
            _db.Pets.Add(pet);
            pets.Add(pet);
        }

        // EF Core wraps this multi-entity SaveChanges in one transaction.
        await _db.SaveChangesAsync(ct);
        return new CustomerRegistrationResult(owner, pets);
    }

    internal static PetInput ToPetInput(CustomerPetInput input)
    {
        if (!ChipStatusOptions.All.Contains(input.ChipStatus))
            throw AppException.Validation("晶片狀態不正確");

        string? chipNumber = null;
        string? unregisteredIdMethod = null;
        switch (input.ChipStatus)
        {
            case ChipStatusOptions.HasChip:
                if (string.IsNullOrWhiteSpace(input.ChipData))
                    throw AppException.Validation("選擇有晶片時，晶片號碼為必填");
                chipNumber = input.ChipData.Trim();
                break;
            case ChipStatusOptions.NoChip:
                unregisteredIdMethod = string.IsNullOrWhiteSpace(input.ChipData)
                    ? ChipStatusOptions.NoChip
                    : input.ChipData.Trim();
                break;
        }

        return new PetInput
        {
            Name = input.Name,
            Species = input.Species,
            Breed = input.Breed,
            Gender = input.Gender,
            Age = input.Age,
            IsNeutered = input.IsNeutered,
            ChipNumber = chipNumber,
            UnregisteredIdMethod = unregisteredIdMethod,
            Personality = new List<string>(input.Personality),
            MedicalHistory = new List<string>(input.MedicalHistory),
            MedicalHistoryOther = input.MedicalHistoryOther,
            PhysicalExamination = new PhysicalExamination(),
            Note = input.Note,
        };
    }

    private static string NormalizeNationalId(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string NormalizePhone(string value)
        => new(value.Where(char.IsDigit).ToArray());
}
