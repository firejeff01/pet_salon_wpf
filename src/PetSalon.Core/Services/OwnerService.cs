using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Constants;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;

namespace PetSalon.Core.Services;

public sealed class OwnerService
{
    private readonly IPetSalonDbContext _db;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public OwnerService(IPetSalonDbContext db, IIdGenerator ids, IClock clock)
    {
        _db = db;
        _ids = ids;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Owner>> ListAsync(string? keyword = null, CancellationToken ct = default)
    {
        var q = _db.Owners.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(o => o.Name.Contains(k) || o.Phone.Contains(k) || o.NationalId.Contains(k));
        }
        return await q.OrderBy(o => o.Name).ToListAsync(ct);
    }

    public async Task<Owner> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var o = await _db.Owners.FindAsync(new object[] { id }, ct)
            ?? throw AppException.NotFound("OWNER_NOT_FOUND", $"飼主 {id} 不存在");
        return o;
    }

    public async Task<IReadOnlyList<Pet>> ListPetsAsync(string ownerId, CancellationToken ct = default)
    {
        _ = await GetByIdAsync(ownerId, ct);
        return await _db.Pets.AsNoTracking().Where(p => p.OwnerId == ownerId).OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<Owner> CreateAsync(OwnerInput input, CancellationToken ct = default)
    {
        Validate(input);
        var now = _clock.Now;
        var owner = new Owner
        {
            OwnerId = _ids.New("owner"),
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(owner, input);
        _db.Owners.Add(owner);
        await _db.SaveChangesAsync(ct);
        return owner;
    }

    public async Task<Owner> UpdateAsync(string id, OwnerInput input, CancellationToken ct = default)
    {
        Validate(input);
        var owner = await _db.Owners.FindAsync(new object[] { id }, ct)
            ?? throw AppException.NotFound("OWNER_NOT_FOUND", $"飼主 {id} 不存在");
        Apply(owner, input);
        owner.UpdatedAt = _clock.Now;
        await _db.SaveChangesAsync(ct);
        return owner;
    }

    public async Task<Owner> AdjustStoredValueAsync(string id, decimal delta, CancellationToken ct = default)
    {
        var owner = await _db.Owners.FindAsync(new object[] { id }, ct)
            ?? throw AppException.NotFound("OWNER_NOT_FOUND", $"飼主 {id} 不存在");
        var next = owner.StoredValueBalance + delta;
        if (next < 0)
            throw AppException.Unprocessable("STORED_VALUE_NEGATIVE", "儲值餘額不可為負數");
        owner.StoredValueBalance = next;
        if (next == 0) owner.IsStoredValueCustomer = false;
        owner.UpdatedAt = _clock.Now;
        await _db.SaveChangesAsync(ct);
        return owner;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var owner = await _db.Owners.FirstOrDefaultAsync(o => o.OwnerId == id, ct)
            ?? throw AppException.NotFound("OWNER_NOT_FOUND", $"飼主 {id} 不存在");
        var pets = await _db.Pets.Where(p => p.OwnerId == id).ToListAsync(ct);
        var petIds = pets.Select(p => p.PetId).ToList();
        var hasHistory = await _db.Appointments.AnyAsync(
            a => a.OwnerId == id || petIds.Contains(a.PetId), ct);
        if (hasHistory)
            throw AppException.Unprocessable(
                "OWNER_HAS_HISTORY",
                "該飼主已有預約或美容紀錄，無法永久刪除");

        _db.Pets.RemoveRange(pets);
        _db.Owners.Remove(owner);
        await _db.SaveChangesAsync(ct);
    }

    internal static void Validate(OwnerInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw AppException.Validation("姓名為必填");
        if (string.IsNullOrWhiteSpace(input.NationalId)) throw AppException.Validation("身分證字號為必填");
        if (string.IsNullOrWhiteSpace(input.Phone)) throw AppException.Validation("聯絡電話為必填");
        if (string.IsNullOrWhiteSpace(input.Address)) throw AppException.Validation("地址為必填");
        // R1 業主決議：緊急聯絡人三欄改為非必填，service 層不再驗證
        if (input.StoredValueBalance < 0) throw AppException.Validation("儲值餘額不可為負數");
    }

    internal static void Apply(Owner owner, OwnerInput input)
    {
        owner.Name = input.Name.Trim();
        owner.NationalId = input.NationalId.Trim();
        owner.Phone = input.Phone.Trim();
        owner.Address = input.Address.Trim();
        owner.EmergencyContactName = input.EmergencyContactName?.Trim() ?? string.Empty;
        owner.EmergencyContactPhone = input.EmergencyContactPhone?.Trim() ?? string.Empty;
        owner.EmergencyContactRelationship = input.EmergencyContactRelationship?.Trim() ?? string.Empty;
        owner.PreferredAnimalHospitalName = string.IsNullOrWhiteSpace(input.PreferredAnimalHospitalName)
            ? DefaultHospital.Name : input.PreferredAnimalHospitalName.Trim();
        owner.PreferredAnimalHospitalPhone = string.IsNullOrWhiteSpace(input.PreferredAnimalHospitalPhone)
            ? DefaultHospital.Phone : input.PreferredAnimalHospitalPhone.Trim();
        owner.PreferredAnimalHospitalAddress = string.IsNullOrWhiteSpace(input.PreferredAnimalHospitalAddress)
            ? DefaultHospital.Address : input.PreferredAnimalHospitalAddress.Trim();
        owner.IsStoredValueCustomer = input.IsStoredValueCustomer;
        owner.StoredValueBalance = input.StoredValueBalance;
        owner.Note = input.Note?.Trim() ?? string.Empty;
    }
}
