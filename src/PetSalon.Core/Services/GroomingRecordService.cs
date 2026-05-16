using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Constants;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;

namespace PetSalon.Core.Services;

public sealed class GroomingRecordService
{
    private readonly IPetSalonDbContext _db;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly StoredValueService _storedValue;

    public GroomingRecordService(
        IPetSalonDbContext db,
        IIdGenerator ids,
        IClock clock,
        StoredValueService storedValue)
    {
        _db = db;
        _ids = ids;
        _clock = clock;
        _storedValue = storedValue;
    }

    public async Task<GroomingRecord?> FindByAppointmentIdAsync(string appointmentId, CancellationToken ct = default)
        => await _db.GroomingRecords
            .Include(g => g.Appointment)!.ThenInclude(a => a!.Owner)
            .Include(g => g.Appointment)!.ThenInclude(a => a!.Pet)
            .FirstOrDefaultAsync(g => g.AppointmentId == appointmentId, ct);

    public async Task<GroomingRecord> GetByIdAsync(string id, CancellationToken ct = default)
        => await _db.GroomingRecords
            .Include(g => g.Appointment)!.ThenInclude(a => a!.Owner)
            .Include(g => g.Appointment)!.ThenInclude(a => a!.Pet)
            .FirstOrDefaultAsync(g => g.GroomingRecordId == id, ct)
            ?? throw AppException.NotFound("RECORD_NOT_FOUND", $"美容紀錄 {id} 不存在");

    public StoredValueCalc PreviewCost(decimal balance, decimal cost) => _storedValue.Calculate(balance, cost);

    public async Task<GroomingRecord> SaveAsync(GroomingRecordInput input, CancellationToken ct = default)
    {
        Validate(input);
        var appt = await _db.Appointments
            .Include(a => a.Owner)
            .Include(a => a.Pet)
            .FirstOrDefaultAsync(a => a.AppointmentId == input.AppointmentId, ct)
            ?? throw AppException.Conflict("APPOINTMENT_NOT_FOUND", $"預約 {input.AppointmentId} 不存在");

        var existing = await _db.GroomingRecords.FirstOrDefaultAsync(g => g.AppointmentId == input.AppointmentId, ct);
        var totalCost = input.Services.Sum(s => s.Price);
        var owner = appt.Owner ?? throw AppException.Conflict("OWNER_NOT_FOUND", "找不到對應的飼主");

        decimal deduction = 0, cash = totalCost;
        if (owner.IsStoredValueCustomer && owner.StoredValueBalance > 0)
        {
            var calc = _storedValue.Calculate(owner.StoredValueBalance, totalCost);
            deduction = calc.Deduction;
            cash = calc.Cash;
        }

        var now = _clock.Now;
        if (existing is null)
        {
            existing = new GroomingRecord
            {
                GroomingRecordId = _ids.New("rec"),
                AppointmentId = appt.AppointmentId,
                CreatedAt = now,
            };
            _db.GroomingRecords.Add(existing);
        }
        else
        {
            // 還原舊扣款，再依新總額重新扣
            if (existing.StoredValueDeduction > 0)
                owner.StoredValueBalance += existing.StoredValueDeduction;
        }

        if (deduction > 0)
        {
            owner.StoredValueBalance -= deduction;
            if (owner.StoredValueBalance < 0) owner.StoredValueBalance = 0;
            if (owner.StoredValueBalance == 0) owner.IsStoredValueCustomer = false;
            owner.UpdatedAt = now;
        }

        existing.ServiceDate = appt.Date;
        existing.ServiceTime = appt.Time;
        existing.Services = input.Services.Select(s => new GroomingServiceItem { Item = s.Item, Price = s.Price }).ToList();
        existing.TotalCost = totalCost;
        existing.StoredValueDeduction = deduction;
        existing.CashPayment = cash;
        existing.PhysicalExamination = new PhysicalExamination
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
        existing.Personality = new List<string>(input.Personality);
        existing.MedicalHistory = new List<string>(input.MedicalHistory ?? new());
        existing.MedicalHistoryOther = input.MedicalHistoryOther ?? string.Empty;
        existing.OwnerNotes = input.OwnerNotes ?? string.Empty;
        existing.ShopNotes = input.ShopNotes ?? string.Empty;
        existing.OtherNotes = input.OtherNotes ?? string.Empty;
        existing.UpdatedAt = now;

        // 標記預約完成
        if (appt.Status == Enums.AppointmentStatus.Booked) appt.Status = Enums.AppointmentStatus.Completed;
        appt.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    private static void Validate(GroomingRecordInput input)
    {
        if (input.Services is null || input.Services.Count == 0)
            throw AppException.Validation("至少需勾選一項服務");
        foreach (var s in input.Services)
        {
            if (!GroomingServiceOptions.IsValid(s.Item))
                throw AppException.Validation($"服務項目「{s.Item}」不在允許清單中");
            if (s.Price < 0)
                throw AppException.Validation("金額不可為負數");
        }
        foreach (var p in input.Personality ?? new())
            if (!PersonalityOptions.IsValid(p))
                throw AppException.Validation($"個性「{p}」不在允許清單中");
        foreach (var m in input.MedicalHistory ?? new())
            if (!MedicalHistoryOptions.IsValid(m))
                throw AppException.Validation($"病史「{m}」不在允許清單中");
    }
}
