using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

public class HomeViewModelTests : VmTestBase
{
    private readonly FakeNavigationService _nav = new();
    private HomeViewModel CreateVm() => new(ScopeFactory, _nav);

    [Fact]
    public async Task InitializeAsync_loads_zero_counts_on_empty_db()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.OwnerCount.Should().Be(0);
        vm.PetCount.Should().Be(0);
        vm.TodayCount.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_loads_actual_counts()
    {
        await InScopeAsync<int>(async sp =>
        {
            var ownerSvc = sp.GetRequiredService<OwnerService>();
            var petSvc = sp.GetRequiredService<PetService>();
            var apptSvc = sp.GetRequiredService<AppointmentService>();
            var owner = await ownerSvc.CreateAsync(new OwnerInput
            {
                Name = "A", NationalId = "X", Phone = "1", Address = "A",
                EmergencyContactName = "B", EmergencyContactPhone = "2", EmergencyContactRelationship = "C",
            });
            var pet = await petSvc.CreateAsync(new PetInput
            {
                OwnerId = owner.OwnerId, Name = "p", Species = "犬", Breed = "x",
                Gender = "公", Age = "1", IsNeutered = false,
                Personality = new() { "親人" }, MedicalHistory = new(),
                PhysicalExamination = new PhysicalExamination(),
            });
            await apptSvc.CreateAsync(new AppointmentCreateInput
            {
                PetId = pet.PetId,
                Date = DateOnly.FromDateTime(DateTime.Today),
                Time = new TimeOnly(10, 0),
            });
            return 0;
        });

        var vm = CreateVm();
        await vm.InitializeAsync();

        vm.OwnerCount.Should().Be(1);
        vm.PetCount.Should().Be(1);
        vm.TodayCount.Should().Be(1);
        vm.TodayAppointments.Should().ContainSingle();
    }

    [Fact]
    public void Greeting_returns_one_of_three_phrases()
    {
        var vm = CreateVm();
        vm.Greeting.Should().BeOneOf("早安", "午安", "晚安");
    }

    [Fact]
    public void TodayDateLabel_contains_chinese_weekday_after_initialize()
    {
        // 觸發 TodayDateLabel 賦值
        var vm = CreateVm();
        _ = vm.InitializeAsync().Wait(1000);
        vm.TodayDateLabel.Should().Contain("週");
    }

    [Fact]
    public void GoOwners_navigates_to_OwnerPage()
    {
        var vm = CreateVm();
        vm.GoOwnersCommand.Execute(null);
        _nav.Visited.Should().Contain(typeof(OwnerPageViewModel));
    }

    [Fact]
    public void GoCalendar_navigates_to_CalendarViewModel()
    {
        var vm = CreateVm();
        vm.GoCalendarCommand.Execute(null);
        _nav.Visited.Should().Contain(typeof(CalendarViewModel));
    }

    [Fact]
    public void GoToday_navigates_to_DailyAppointments()
    {
        var vm = CreateVm();
        vm.GoTodayCommand.Execute(null);
        _nav.Visited.Should().Contain(typeof(DailyAppointmentsViewModel));
    }

    [Fact]
    public void GoCustomer_navigates_to_CustomerForm()
    {
        var vm = CreateVm();
        vm.GoCustomerCommand.Execute(null);
        _nav.Visited.Should().Contain(typeof(CustomerFormViewModel));
    }

    [Fact]
    public void GoBackup_navigates_to_BackupPage()
    {
        var vm = CreateVm();
        vm.GoBackupCommand.Execute(null);
        _nav.Visited.Should().Contain(typeof(BackupPageViewModel));
    }
}
