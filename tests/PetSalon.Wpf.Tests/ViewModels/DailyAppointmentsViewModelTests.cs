using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

public class DailyAppointmentsViewModelTests : VmTestBase
{
    private readonly FakeNavigationService _nav = new();
    private readonly FakeDialogService _defaultDialog = new();
    private DailyAppointmentsViewModel CreateVm() => new(ScopeFactory, _nav, _defaultDialog);

    private async Task<string> CreateAppointmentOnAsync(DateOnly date)
    {
        return await InScopeAsync(async sp =>
        {
            var owner = await sp.GetRequiredService<OwnerService>().CreateAsync(new OwnerInput
            {
                Name = "x", NationalId = "A", Phone = "B", Address = "C",
                EmergencyContactName = "D", EmergencyContactPhone = "E", EmergencyContactRelationship = "F",
            });
            var pet = await sp.GetRequiredService<PetService>().CreateAsync(new PetInput
            {
                OwnerId = owner.OwnerId, Name = "p", Species = "犬", Breed = "x",
                Gender = "公", Age = "1", IsNeutered = false,
                Personality = new() { "親人" }, MedicalHistory = new(),
                PhysicalExamination = new PhysicalExamination(),
            });
            var appt = await sp.GetRequiredService<AppointmentService>().CreateAsync(new AppointmentCreateInput
            {
                PetId = pet.PetId, Date = date, Time = new TimeOnly(10, 0),
            });
            return appt.AppointmentId;
        });
    }

    [Fact]
    public void Constructor_defaults_to_today()
    {
        var vm = CreateVm();
        vm.Date.Should().Be(DateOnly.FromDateTime(DateTime.Today));
    }

    [Fact]
    public async Task Setting_Date_reloads_appointments()
    {
        var apptId = await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        var vm = CreateVm();
        vm.Date = new DateOnly(2026, 6, 1);

        // OnDateChanged async fire-and-forget; wait
        for (int i = 0; i < 10 && vm.Appointments.Count == 0; i++) await Task.Delay(50);
        vm.Appointments.Should().ContainSingle().Which.AppointmentId.Should().Be(apptId);
    }

    [Fact]
    public async Task Setting_different_date_returns_empty()
    {
        await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        var vm = CreateVm();
        vm.Date = new DateOnly(2026, 6, 2);
        await vm.ReloadCommand.ExecuteAsync(null);
        vm.Appointments.Should().BeEmpty();
    }

    [Fact]
    public void Back_navigates_to_Calendar()
    {
        var vm = CreateVm();
        vm.BackCommand.Execute(null);
        _nav.Visited.Should().Contain(typeof(CalendarViewModel));
    }

    [Fact]
    public async Task OpenGrooming_navigates_to_GroomingPage()
    {
        var apptId = await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        var vm = CreateVm();
        vm.Date = new DateOnly(2026, 6, 1);
        await vm.ReloadCommand.ExecuteAsync(null);

        vm.OpenGroomingCommand.Execute(vm.Appointments.First());
        _nav.Visited.Should().Contain(typeof(GroomingPageViewModel));
    }

    [Fact]
    public void OpenGrooming_null_appointment_is_noop()
    {
        var vm = CreateVm();
        vm.OpenGroomingCommand.Execute(null);
        _nav.Visited.Should().NotContain(typeof(GroomingPageViewModel));
    }

    // ============ TDD #1: 預約狀態變更 + 取消原因 ============

    [Fact]
    public async Task MarkComplete_changes_status_to_Completed_and_reloads()
    {
        var apptId = await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        var dlg = new FakeDialogService();
        var vm = new DailyAppointmentsViewModel(ScopeFactory, _nav, dlg);
        vm.Date = new DateOnly(2026, 6, 1);
        await vm.ReloadCommand.ExecuteAsync(null);
        vm.Selected = vm.Appointments.First();

        await vm.MarkCompleteCommand.ExecuteAsync(null);

        dlg.Successes.Should().ContainSingle().Which.message.Should().Contain("已完成");
        var appt = await InScopeAsync(sp => sp.GetRequiredService<AppointmentService>().GetByIdAsync(apptId));
        appt.Status.Should().Be(PetSalon.Core.Enums.AppointmentStatus.Completed);
    }

    [Fact]
    public async Task MarkComplete_no_selection_shows_error()
    {
        var dlg = new FakeDialogService();
        var vm = new DailyAppointmentsViewModel(ScopeFactory, _nav, dlg);
        await vm.MarkCompleteCommand.ExecuteAsync(null);
        dlg.Errors.Should().ContainSingle().Which.title.Should().Be("尚未選取");
    }

    [Fact]
    public async Task CancelAppointment_with_reason_changes_status()
    {
        var apptId = await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        var dlg = new FakeDialogService();
        var vm = new DailyAppointmentsViewModel(ScopeFactory, _nav, dlg);
        vm.Date = new DateOnly(2026, 6, 1);
        await vm.ReloadCommand.ExecuteAsync(null);
        vm.Selected = vm.Appointments.First();
        vm.CancelReason = "客戶臨時有事";

        await vm.CancelAppointmentCommand.ExecuteAsync(null);

        var appt = await InScopeAsync(sp => sp.GetRequiredService<AppointmentService>().GetByIdAsync(apptId));
        appt.Status.Should().Be(PetSalon.Core.Enums.AppointmentStatus.Cancelled);
        appt.CancelReason.Should().Be("客戶臨時有事");
        dlg.Successes.Should().ContainSingle();
    }

    [Fact]
    public async Task CancelAppointment_no_reason_shows_error()
    {
        var apptId = await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        var dlg = new FakeDialogService();
        var vm = new DailyAppointmentsViewModel(ScopeFactory, _nav, dlg);
        vm.Date = new DateOnly(2026, 6, 1);
        await vm.ReloadCommand.ExecuteAsync(null);
        vm.Selected = vm.Appointments.First();
        vm.CancelReason = "";

        await vm.CancelAppointmentCommand.ExecuteAsync(null);

        dlg.Errors.Should().ContainSingle().Which.message.Should().Contain("取消原因");
        var appt = await InScopeAsync(sp => sp.GetRequiredService<AppointmentService>().GetByIdAsync(apptId));
        appt.Status.Should().Be(PetSalon.Core.Enums.AppointmentStatus.Booked);
    }

    // ============ TDD #2: 預約刪除 ============

    [Fact]
    public async Task Delete_removes_appointment_after_confirm()
    {
        var apptId = await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        var dlg = new FakeDialogService { ConfirmResponse = (_, _) => true };
        var vm = new DailyAppointmentsViewModel(ScopeFactory, _nav, dlg);
        vm.Date = new DateOnly(2026, 6, 1);
        await vm.ReloadCommand.ExecuteAsync(null);
        vm.Selected = vm.Appointments.First();

        await vm.DeleteCommand.ExecuteAsync(null);

        dlg.Confirms.Should().ContainSingle();
        vm.Appointments.Should().BeEmpty();
        dlg.Successes.Should().ContainSingle().Which.message.Should().Contain("刪除");
    }

    [Fact]
    public async Task Delete_user_cancels_keeps_appointment()
    {
        var apptId = await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        var dlg = new FakeDialogService { ConfirmResponse = (_, _) => false };
        var vm = new DailyAppointmentsViewModel(ScopeFactory, _nav, dlg);
        vm.Date = new DateOnly(2026, 6, 1);
        await vm.ReloadCommand.ExecuteAsync(null);
        vm.Selected = vm.Appointments.First();

        await vm.DeleteCommand.ExecuteAsync(null);

        dlg.Confirms.Should().ContainSingle();
        vm.Appointments.Should().ContainSingle();
        dlg.Successes.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_blocked_when_grooming_record_exists()
    {
        var apptId = await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1));
        await InScopeAsync<int>(async sp =>
        {
            await sp.GetRequiredService<GroomingRecordService>().SaveAsync(new GroomingRecordInput
            {
                AppointmentId = apptId,
                Services = new() { new() { Item = "洗澡", Price = 500 } },
                Personality = new() { "親人" },
                MedicalHistory = new(),
                PhysicalExamination = new PhysicalExamination(),
            });
            return 0;
        });
        var dlg = new FakeDialogService { ConfirmResponse = (_, _) => true };
        var vm = new DailyAppointmentsViewModel(ScopeFactory, _nav, dlg);
        vm.Date = new DateOnly(2026, 6, 1);
        await vm.ReloadCommand.ExecuteAsync(null);
        vm.Selected = vm.Appointments.First();

        await vm.DeleteCommand.ExecuteAsync(null);

        dlg.Errors.Should().ContainSingle().Which.message.Should().Contain("美容紀錄");
    }
}
