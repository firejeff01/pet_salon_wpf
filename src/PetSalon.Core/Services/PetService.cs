using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Constants;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Enums;

namespace PetSalon.Core.Services;

public sealed class PetService
{
    private readonly IPetSalonDbContext _db;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public PetService(IPetSalonDbContext db, IIdGenerator ids, IClock clock)
    {
        _db = db;
        _ids = ids;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Pet>> ListAsync(string? ownerId = null, string? keyword = null, CancellationToken ct = default)
    {
        var q = _db.Pets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(ownerId)) q = q.Where(p => p.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(p => p.Name.Contains(k) || p.Breed.Contains(k));
        }
        return await q.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<Pet> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var p = await _db.Pets.Include(x => x.Owner).FirstOrDefaultAsync(x => x.PetId == id, ct)
            ?? throw AppException.NotFound("PET_NOT_FOUND", $"寵物 {id} 不存在");
        return p;
    }

    public async Task<Pet> CreateAsync(PetInput input, CancellationToken ct = default)
    {
        Validate(input);
        var ownerExists = await _db.Owners.AnyAsync(o => o.OwnerId == input.OwnerId, ct);
        if (!ownerExists) throw AppException.Conflict("OWNER_NOT_FOUND", $"飼主 {input.OwnerId} 不存在");

        var now = _clock.Now;
        var pet = new Pet
        {
            PetId = _ids.New("pet"),
            OwnerId = input.OwnerId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(pet, input);
        _db.Pets.Add(pet);
        await _db.SaveChangesAsync(ct);
        return pet;
    }

    public async Task<Pet> UpdateAsync(string id, PetInput input, CancellationToken ct = default)
    {
        Validate(input);
        var pet = await _db.Pets.FindAsync(new object[] { id }, ct)
            ?? throw AppException.NotFound("PET_NOT_FOUND", $"寵物 {id} 不存在");
        Apply(pet, input);
        pet.UpdatedAt = _clock.Now;
        await _db.SaveChangesAsync(ct);
        return pet;
    }

    private static void Validate(PetInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw AppException.Validation("寵物名稱為必填");
        if (!PetSpecies.All.Contains(input.Species)) throw AppException.Validation("物種須為犬或貓");
        if (string.IsNullOrWhiteSpace(input.Breed)) throw AppException.Validation("品種為必填");
        if (!PetGender.All.Contains(input.Gender)) throw AppException.Validation("性別須為公或母");
        if (string.IsNullOrWhiteSpace(input.Age)) throw AppException.Validation("年齡為必填");
        if (input.Personality is null || input.Personality.Count == 0)
            throw AppException.Validation("個性至少需勾選一項");
        foreach (var p in input.Personality)
            if (!PersonalityOptions.IsValid(p))
                throw AppException.Validation($"個性「{p}」不在允許清單中");
        foreach (var m in input.MedicalHistory ?? new())
            if (!MedicalHistoryOptions.IsValid(m))
                throw AppException.Validation($"病史「{m}」不在允許清單中");
    }

    private static void Apply(Pet pet, PetInput input)
    {
        pet.Name = input.Name.Trim();
        pet.Species = input.Species;
        pet.Breed = input.Breed.Trim();
        pet.Gender = input.Gender;
        pet.Age = input.Age.Trim();
        pet.IsNeutered = input.IsNeutered;
        pet.ChipNumber = string.IsNullOrWhiteSpace(input.ChipNumber) ? null : input.ChipNumber.Trim();
        pet.UnregisteredIdMethod = string.IsNullOrWhiteSpace(input.UnregisteredIdMethod) ? null : input.UnregisteredIdMethod.Trim();
        pet.Personality = new List<string>(input.Personality);
        pet.PhysicalExamination = new PhysicalExamination
        {
            Eyes = input.PhysicalExamination.Eyes,
            Ears = input.PhysicalExamination.Ears,
            Teeth = input.PhysicalExamination.Teeth,
            Limbs = input.PhysicalExamination.Limbs,
            Skin = input.PhysicalExamination.Skin,
            Fur = input.PhysicalExamination.Fur,
            EyesNote = input.PhysicalExamination.EyesNote,
            EarsNote = input.PhysicalExamination.EarsNote,
            TeethNote = input.PhysicalExamination.TeethNote,
            LimbsNote = input.PhysicalExamination.LimbsNote,
            SkinNote = input.PhysicalExamination.SkinNote,
            FurNote = input.PhysicalExamination.FurNote,
        };
        pet.MedicalHistory = new List<string>(input.MedicalHistory ?? new());
        pet.MedicalHistoryOther = input.MedicalHistoryOther?.Trim() ?? string.Empty;
        pet.Note = input.Note?.Trim() ?? string.Empty;
    }
}
