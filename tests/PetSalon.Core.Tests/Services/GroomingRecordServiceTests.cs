using FluentAssertions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Enums;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public class GroomingRecordServiceTests : ServiceTestBase
{
    private OwnerService OwnerSvc => new(Db, Ids, Clock);
    private PetService PetSvc => new(Db, Ids, Clock);
    private AppointmentService ApptSvc => new(Db, Ids, Clock);
    private GroomingRecordService GroomingSvc => new(Db, Ids, Clock, new StoredValueService());

    private async Task<(string ownerId, string petId, string apptId)> SetupAsync(bool storedValue = false, decimal balance = 0)
    {
        var input = new OwnerInput
        {
            Name = "張三", NationalId = "A1", Phone = "0912",
            Address = "桃園", EmergencyContactName = "聯絡人",
            EmergencyContactPhone = "0987", EmergencyContactRelationship = "配偶",
            IsStoredValueCustomer = storedValue, StoredValueBalance = balance,
        };
        var owner = await OwnerSvc.CreateAsync(input);
        var pet = await PetSvc.CreateAsync(new PetInput
        {
            OwnerId = owner.OwnerId, Name = "毛毛", Species = "犬", Breed = "柴犬",
            Gender = "公", Age = "3", IsNeutered = true,
            Personality = new() { "親人" }, MedicalHistory = new(),
            PhysicalExamination = new PhysicalExamination(),
        });
        var appt = await ApptSvc.CreateAsync(new AppointmentCreateInput
        {
            PetId = pet.PetId,
            Date = new DateOnly(2026, 6, 1),
            Time = new TimeOnly(10, 0),
        });
        return (owner.OwnerId, pet.PetId, appt.AppointmentId);
    }

    private static GroomingRecordInput InputFor(string apptId) => new()
    {
        AppointmentId = apptId,
        Services = new() { new() { Item = "洗澡", Price = 500 } },
        Personality = new() { "親人" },
        MedicalHistory = new(),
        PhysicalExamination = new PhysicalExamination(),
        OwnerNotes = string.Empty,
        ShopNotes = string.Empty,
        OtherNotes = string.Empty,
    };

    // ===================== Save (Create) =====================

    [Fact]
    public async Task Save_creates_record_with_generated_id_and_total_cost()
    {
        var (_, _, apptId) = await SetupAsync();
        var input = InputFor(apptId);
        input.Services = new()
        {
            new() { Item = "洗澡", Price = 500 },
            new() { Item = "美容", Price = 800 },
        };

        var rec = await GroomingSvc.SaveAsync(input);
        rec.GroomingRecordId.Should().StartWith("rec_");
        rec.TotalCost.Should().Be(1300);
        rec.Services.Should().HaveCount(2);
    }

    [Fact]
    public async Task Save_inherits_service_date_time_from_appointment()
    {
        var (_, _, apptId) = await SetupAsync();
        var rec = await GroomingSvc.SaveAsync(InputFor(apptId));
        rec.ServiceDate.Should().Be(new DateOnly(2026, 6, 1));
        rec.ServiceTime.Should().Be(new TimeOnly(10, 0));
    }

    [Fact]
    public async Task Save_marks_appointment_as_Completed()
    {
        var (_, _, apptId) = await SetupAsync();
        await GroomingSvc.SaveAsync(InputFor(apptId));

        var appt = await ApptSvc.GetByIdAsync(apptId);
        appt.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Save_fails_when_appointment_missing()
    {
        var input = InputFor("nope");
        await FluentActions.Awaiting(() => GroomingSvc.SaveAsync(input))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "APPOINTMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Save_rejects_empty_services()
    {
        var (_, _, apptId) = await SetupAsync();
        var input = InputFor(apptId);
        input.Services = new();
        await FluentActions.Awaiting(() => GroomingSvc.SaveAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*至少*服務*");
    }

    [Fact]
    public async Task Save_rejects_invalid_service_item()
    {
        var (_, _, apptId) = await SetupAsync();
        var input = InputFor(apptId);
        input.Services = new() { new() { Item = "未知服務", Price = 100 } };
        await FluentActions.Awaiting(() => GroomingSvc.SaveAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*服務項目*未知服務*允許*");
    }

    [Fact]
    public async Task Save_rejects_negative_price()
    {
        var (_, _, apptId) = await SetupAsync();
        var input = InputFor(apptId);
        input.Services = new() { new() { Item = "洗澡", Price = -100 } };
        await FluentActions.Awaiting(() => GroomingSvc.SaveAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*金額*負數*");
    }

    [Fact]
    public async Task Save_rejects_invalid_medical_history_option()
    {
        var (_, _, apptId) = await SetupAsync();
        var input = InputFor(apptId);
        input.MedicalHistory = new() { "不存在的病" };
        await FluentActions.Awaiting(() => GroomingSvc.SaveAsync(input))
            .Should().ThrowAsync<AppException>();
    }

    // ===================== Stored value 扣抵 =====================

    [Fact]
    public async Task Save_deducts_stored_value_when_balance_covers_total()
    {
        var (ownerId, _, apptId) = await SetupAsync(storedValue: true, balance: 2000);
        var input = InputFor(apptId);
        input.Services = new() { new() { Item = "洗澡", Price = 500 } };

        var rec = await GroomingSvc.SaveAsync(input);
        rec.StoredValueDeduction.Should().Be(500);
        rec.CashPayment.Should().Be(0);

        var owner = await OwnerSvc.GetByIdAsync(ownerId);
        owner.StoredValueBalance.Should().Be(1500);
        owner.IsStoredValueCustomer.Should().BeTrue();
    }

    [Fact]
    public async Task Save_deducts_partial_when_balance_insufficient()
    {
        var (ownerId, _, apptId) = await SetupAsync(storedValue: true, balance: 300);
        var input = InputFor(apptId);
        input.Services = new() { new() { Item = "洗澡", Price = 500 } };

        var rec = await GroomingSvc.SaveAsync(input);
        rec.StoredValueDeduction.Should().Be(300);
        rec.CashPayment.Should().Be(200);

        var owner = await OwnerSvc.GetByIdAsync(ownerId);
        owner.StoredValueBalance.Should().Be(0);
        owner.IsStoredValueCustomer.Should().BeFalse();   // 餘額歸零自動關閉
    }

    [Fact]
    public async Task Save_no_deduction_when_not_storedValue_customer()
    {
        var (ownerId, _, apptId) = await SetupAsync(storedValue: false, balance: 5000);
        // 即使 balance 有錢，但旗標關閉 → 不扣抵
        var input = InputFor(apptId);

        var rec = await GroomingSvc.SaveAsync(input);
        rec.StoredValueDeduction.Should().Be(0);
        rec.CashPayment.Should().Be(500);

        var owner = await OwnerSvc.GetByIdAsync(ownerId);
        owner.StoredValueBalance.Should().Be(5000);   // 不動
    }

    [Fact]
    public async Task Save_updating_existing_record_restores_old_deduction_then_re_applies()
    {
        var (ownerId, _, apptId) = await SetupAsync(storedValue: true, balance: 1000);
        // 第一次：扣 500
        await GroomingSvc.SaveAsync(InputFor(apptId));
        (await OwnerSvc.GetByIdAsync(ownerId)).StoredValueBalance.Should().Be(500);

        // 第二次：總額 200，應該還原 +500 再扣 200 → 餘額 800
        var input2 = InputFor(apptId);
        input2.Services = new() { new() { Item = "洗澡", Price = 200 } };
        var rec2 = await GroomingSvc.SaveAsync(input2);
        rec2.StoredValueDeduction.Should().Be(200);
        rec2.TotalCost.Should().Be(200);
        (await OwnerSvc.GetByIdAsync(ownerId)).StoredValueBalance.Should().Be(800);
    }

    [Fact]
    public async Task Save_updating_existing_record_keeps_id()
    {
        var (_, _, apptId) = await SetupAsync();
        var first = await GroomingSvc.SaveAsync(InputFor(apptId));
        var second = await GroomingSvc.SaveAsync(InputFor(apptId));
        second.GroomingRecordId.Should().Be(first.GroomingRecordId);
    }

    // ===================== FindByAppointmentId / GetById =====================

    [Fact]
    public async Task FindByAppointmentIdAsync_returns_null_when_no_record()
    {
        var (_, _, apptId) = await SetupAsync();
        (await GroomingSvc.FindByAppointmentIdAsync(apptId)).Should().BeNull();
    }

    [Fact]
    public async Task FindByAppointmentIdAsync_returns_existing()
    {
        var (_, _, apptId) = await SetupAsync();
        var saved = await GroomingSvc.SaveAsync(InputFor(apptId));
        var found = await GroomingSvc.FindByAppointmentIdAsync(apptId);
        found.Should().NotBeNull();
        found!.GroomingRecordId.Should().Be(saved.GroomingRecordId);
        found.Appointment.Should().NotBeNull();
        found.Appointment!.Pet.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_throws_when_not_found()
    {
        await FluentActions.Awaiting(() => GroomingSvc.GetByIdAsync("missing"))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "RECORD_NOT_FOUND");
    }

    // ===================== PreviewCost (公開計算用) =====================

    [Fact]
    public void PreviewCost_uses_StoredValueService()
    {
        var calc = GroomingSvc.PreviewCost(500m, 800m);
        calc.Deduction.Should().Be(500);
        calc.Cash.Should().Be(300);
        calc.Remaining.Should().Be(0);
    }
}
