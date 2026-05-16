using FluentAssertions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Enums;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public class AppointmentServiceTests : ServiceTestBase
{
    private OwnerService OwnerSvc => new(Db, Ids, Clock);
    private PetService PetSvc => new(Db, Ids, Clock);
    private AppointmentService ApptSvc => new(Db, Ids, Clock);
    private GroomingRecordService GroomingSvc => new(Db, Ids, Clock, new StoredValueService());

    private async Task<(string ownerId, string petId)> CreateOwnerAndPetAsync(string name = "毛毛")
    {
        var owner = await OwnerSvc.CreateAsync(new OwnerInput
        {
            Name = "張三", NationalId = "A1", Phone = "0912",
            Address = "桃園", EmergencyContactName = "聯絡人",
            EmergencyContactPhone = "0987", EmergencyContactRelationship = "配偶",
        });
        var pet = await PetSvc.CreateAsync(new PetInput
        {
            OwnerId = owner.OwnerId, Name = name, Species = "犬", Breed = "柴犬",
            Gender = "公", Age = "3", IsNeutered = true,
            Personality = new() { "親人" }, MedicalHistory = new(),
            PhysicalExamination = new PhysicalExamination(),
        });
        return (owner.OwnerId, pet.PetId);
    }

    private static AppointmentCreateInput InputFor(string petId, DateOnly? date = null, TimeOnly? time = null) => new()
    {
        PetId = petId,
        Date = date ?? new DateOnly(2026, 6, 1),
        Time = time ?? new TimeOnly(10, 0),
        Note = string.Empty,
    };

    // ===================== Create =====================

    [Fact]
    public async Task Create_happy_path_persists_with_status_Booked()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));
        appt.AppointmentId.Should().StartWith("appt_");
        appt.Status.Should().Be(AppointmentStatus.Booked);
        appt.PetId.Should().Be(petId);
    }

    [Fact]
    public async Task Create_resolves_OwnerId_from_Pet_owner()
    {
        var (ownerId, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));
        appt.OwnerId.Should().Be(ownerId);
    }

    [Fact]
    public async Task Create_fails_when_pet_missing()
    {
        await FluentActions.Awaiting(() => ApptSvc.CreateAsync(InputFor("nope")))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "PET_NOT_FOUND");
    }

    [Fact]
    public async Task Create_fails_when_petId_blank()
    {
        await FluentActions.Awaiting(() => ApptSvc.CreateAsync(InputFor("")))
            .Should().ThrowAsync<AppException>().WithMessage("*選擇*寵物*");
    }

    // ===================== Update =====================

    [Fact]
    public async Task Update_modifies_date_time_note()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));

        Clock.Now = Clock.Now.AddHours(1);
        var updated = await ApptSvc.UpdateAsync(appt.AppointmentId, new AppointmentUpdateInput
        {
            Date = new DateOnly(2026, 7, 1),
            Time = new TimeOnly(15, 30),
            Note = "更新備註",
        });
        updated.Date.Should().Be(new DateOnly(2026, 7, 1));
        updated.Time.Should().Be(new TimeOnly(15, 30));
        updated.Note.Should().Be("更新備註");
        updated.UpdatedAt.Should().Be(Clock.Now);
    }

    [Fact]
    public async Task Update_partial_only_changes_provided_fields()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));
        var origDate = appt.Date;

        var updated = await ApptSvc.UpdateAsync(appt.AppointmentId, new AppointmentUpdateInput
        {
            Note = "僅改備註",
        });
        updated.Date.Should().Be(origDate);
        updated.Note.Should().Be("僅改備註");
    }

    [Fact]
    public async Task Update_to_Cancelled_requires_cancel_reason()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));

        await FluentActions.Awaiting(() => ApptSvc.UpdateAsync(appt.AppointmentId, new AppointmentUpdateInput
        {
            Status = AppointmentStatus.Cancelled,
        }))
        .Should().ThrowAsync<AppException>().WithMessage("*取消*原因*");
    }

    [Fact]
    public async Task Update_to_Cancelled_with_reason_succeeds()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));

        var updated = await ApptSvc.UpdateAsync(appt.AppointmentId, new AppointmentUpdateInput
        {
            Status = AppointmentStatus.Cancelled,
            CancelReason = "客戶臨時有事",
        });
        updated.Status.Should().Be(AppointmentStatus.Cancelled);
        updated.CancelReason.Should().Be("客戶臨時有事");
    }

    [Fact]
    public async Task Update_status_invalid_throws()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));

        await FluentActions.Awaiting(() => ApptSvc.UpdateAsync(appt.AppointmentId, new AppointmentUpdateInput
        {
            Status = "不存在的狀態",
        }))
        .Should().ThrowAsync<AppException>().WithMessage("*狀態*");
    }

    [Fact]
    public async Task Update_throws_when_not_found()
    {
        await FluentActions.Awaiting(() => ApptSvc.UpdateAsync("missing", new AppointmentUpdateInput { Note = "x" }))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "APPOINTMENT_NOT_FOUND");
    }

    // ===================== Delete =====================

    [Fact]
    public async Task Delete_removes_appointment_when_no_grooming_record()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));

        await ApptSvc.DeleteAsync(appt.AppointmentId);
        (await ApptSvc.ListAsync(new AppointmentListFilter())).Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_blocked_when_grooming_record_exists()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var appt = await ApptSvc.CreateAsync(InputFor(petId));
        await GroomingSvc.SaveAsync(new GroomingRecordInput
        {
            AppointmentId = appt.AppointmentId,
            Services = new() { new() { Item = "洗澡", Price = 500 } },
            Personality = new() { "親人" },
            MedicalHistory = new(),
            PhysicalExamination = new PhysicalExamination(),
        });

        await FluentActions.Awaiting(() => ApptSvc.DeleteAsync(appt.AppointmentId))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "APPOINTMENT_HAS_RECORD");
    }

    [Fact]
    public async Task Delete_throws_when_not_found()
    {
        await FluentActions.Awaiting(() => ApptSvc.DeleteAsync("missing"))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "APPOINTMENT_NOT_FOUND");
    }

    // ===================== List / Filter =====================

    [Fact]
    public async Task ListAsync_returns_all_with_navigations_loaded()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        await ApptSvc.CreateAsync(InputFor(petId));

        var list = await ApptSvc.ListAsync(new AppointmentListFilter());
        list.Should().ContainSingle().Which.Pet.Should().NotBeNull();
        list[0].Owner.Should().NotBeNull();
    }

    [Fact]
    public async Task ListAsync_filters_by_date()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 1)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 2)));

        var r = await ApptSvc.ListAsync(new AppointmentListFilter { Date = new DateOnly(2026, 6, 2) });
        r.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 6, 2));
    }

    [Fact]
    public async Task ListAsync_filters_by_status()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        var a1 = await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 1)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 2)));
        await ApptSvc.UpdateAsync(a1.AppointmentId, new AppointmentUpdateInput { Status = AppointmentStatus.Cancelled, CancelReason = "取消" });

        var booked = await ApptSvc.ListAsync(new AppointmentListFilter { Status = AppointmentStatus.Booked });
        booked.Should().ContainSingle().Which.Status.Should().Be(AppointmentStatus.Booked);
    }

    [Fact]
    public async Task ListAsync_filters_by_dateFrom_and_dateTo()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 1)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 15)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 7, 1)));

        var r = await ApptSvc.ListAsync(new AppointmentListFilter
        {
            DateFrom = new DateOnly(2026, 6, 1),
            DateTo = new DateOnly(2026, 6, 30),
        });
        r.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_sorts_by_date_then_time()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 1), new TimeOnly(15, 0)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 1), new TimeOnly(10, 0)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 5, 30), new TimeOnly(12, 0)));

        var list = await ApptSvc.ListAsync(new AppointmentListFilter());
        list[0].Date.Should().Be(new DateOnly(2026, 5, 30));
        list[1].Time.Should().Be(new TimeOnly(10, 0));
        list[2].Time.Should().Be(new TimeOnly(15, 0));
    }

    // ===================== Calendar Summary =====================

    [Fact]
    public async Task CalendarSummary_groups_by_date_with_status_counts()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 1), new TimeOnly(10, 0)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 1), new TimeOnly(14, 0)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 6, 5), new TimeOnly(11, 0)));

        var summary = await ApptSvc.CalendarSummaryAsync(2026, 6);
        summary.Should().HaveCount(2);
        var jun1 = summary.First(s => s.Date == new DateOnly(2026, 6, 1));
        jun1.Count.Should().Be(2);
        jun1.StatusSummary[AppointmentStatus.Booked].Should().Be(2);
    }

    [Fact]
    public async Task CalendarSummary_excludes_other_months()
    {
        var (_, petId) = await CreateOwnerAndPetAsync();
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 5, 31)));
        await ApptSvc.CreateAsync(InputFor(petId, new DateOnly(2026, 7, 1)));

        (await ApptSvc.CalendarSummaryAsync(2026, 6)).Should().BeEmpty();
    }
}
