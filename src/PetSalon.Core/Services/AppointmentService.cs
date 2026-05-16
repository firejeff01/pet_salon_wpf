using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Enums;

namespace PetSalon.Core.Services;

public sealed class AppointmentService
{
    private readonly IPetSalonDbContext _db;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public AppointmentService(IPetSalonDbContext db, IIdGenerator ids, IClock clock)
    {
        _db = db;
        _ids = ids;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Appointment>> ListAsync(AppointmentListFilter filter, CancellationToken ct = default)
    {
        var q = _db.Appointments.AsNoTracking()
            .Include(a => a.Owner)
            .Include(a => a.Pet)
            .Include(a => a.GroomingRecord)
            .AsQueryable();
        if (filter.Date.HasValue) q = q.Where(a => a.Date == filter.Date.Value);
        if (filter.DateFrom.HasValue) q = q.Where(a => a.Date >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue) q = q.Where(a => a.Date <= filter.DateTo.Value);
        if (!string.IsNullOrEmpty(filter.Status)) q = q.Where(a => a.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.PetId)) q = q.Where(a => a.PetId == filter.PetId);
        if (!string.IsNullOrEmpty(filter.OwnerId)) q = q.Where(a => a.OwnerId == filter.OwnerId);
        return await q.OrderBy(a => a.Date).ThenBy(a => a.Time).ToListAsync(ct);
    }

    public async Task<Appointment> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var a = await _db.Appointments
            .Include(x => x.Owner)
            .Include(x => x.Pet)
            .Include(x => x.GroomingRecord)
            .FirstOrDefaultAsync(x => x.AppointmentId == id, ct)
            ?? throw AppException.NotFound("APPOINTMENT_NOT_FOUND", $"預約 {id} 不存在");
        return a;
    }

    public async Task<Appointment> CreateAsync(AppointmentCreateInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(input.PetId)) throw AppException.Validation("請選擇寵物");
        var pet = await _db.Pets.FindAsync(new object[] { input.PetId }, ct)
            ?? throw AppException.NotFound("PET_NOT_FOUND", $"寵物 {input.PetId} 不存在");

        var now = _clock.Now;
        var appt = new Appointment
        {
            AppointmentId = _ids.New("appt"),
            OwnerId = pet.OwnerId,
            PetId = pet.PetId,
            Date = input.Date,
            Time = input.Time,
            Note = input.Note ?? string.Empty,
            Status = AppointmentStatus.Booked,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync(ct);
        return appt;
    }

    public async Task<Appointment> UpdateAsync(string id, AppointmentUpdateInput input, CancellationToken ct = default)
    {
        var appt = await _db.Appointments.FindAsync(new object[] { id }, ct)
            ?? throw AppException.NotFound("APPOINTMENT_NOT_FOUND", $"預約 {id} 不存在");

        if (input.Date.HasValue) appt.Date = input.Date.Value;
        if (input.Time.HasValue) appt.Time = input.Time.Value;
        if (input.Note is not null) appt.Note = input.Note;

        if (input.Status is not null)
        {
            if (!AppointmentStatus.IsValid(input.Status))
                throw AppException.Validation($"狀態須為：{string.Join(',', AppointmentStatus.All)}");
            appt.Status = input.Status;
        }
        if (appt.Status == AppointmentStatus.Cancelled)
        {
            var reason = input.CancelReason ?? appt.CancelReason;
            if (string.IsNullOrWhiteSpace(reason))
                throw AppException.Validation("取消預約須填寫原因");
            appt.CancelReason = reason.Trim();
        }
        else if (input.CancelReason is not null)
        {
            appt.CancelReason = input.CancelReason;
        }

        appt.UpdatedAt = _clock.Now;
        await _db.SaveChangesAsync(ct);
        return appt;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var appt = await _db.Appointments.FindAsync(new object[] { id }, ct)
            ?? throw AppException.NotFound("APPOINTMENT_NOT_FOUND", $"預約 {id} 不存在");
        var hasRec = await _db.GroomingRecords.AnyAsync(g => g.AppointmentId == id, ct);
        if (hasRec)
            throw AppException.Unprocessable("APPOINTMENT_HAS_RECORD", "該預約已有美容紀錄，不可刪除");
        _db.Appointments.Remove(appt);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CalendarSummaryEntry>> CalendarSummaryAsync(int year, int month, CancellationToken ct = default)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1);

        var appts = await _db.Appointments.AsNoTracking()
            .Where(a => a.Date >= first && a.Date < last)
            .Select(a => new { a.Date, a.Status })
            .ToListAsync(ct);

        return appts
            .GroupBy(a => a.Date)
            .Select(g => new CalendarSummaryEntry(
                g.Key,
                g.Count(),
                g.GroupBy(x => x.Status).ToDictionary(x => x.Key, x => x.Count())))
            .OrderBy(e => e.Date)
            .ToList();
    }
}
