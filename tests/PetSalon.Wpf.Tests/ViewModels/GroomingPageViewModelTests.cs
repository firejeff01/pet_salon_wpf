using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

public class GroomingPageViewModelTests : VmTestBase
{
    private readonly FakeDialogService _dialog = new();
    private readonly FakeNavigationService _nav = new();
    private GroomingPageViewModel CreateVm() => new(ScopeFactory, _nav, _dialog);

    private async Task<string> SetupAsync(bool storedValue = false, decimal balance = 0)
    {
        return await InScopeAsync(async sp =>
        {
            var owner = await sp.GetRequiredService<OwnerService>().CreateAsync(new OwnerInput
            {
                Name = "張三", NationalId = "A1", Phone = "0912", Address = "桃園",
                EmergencyContactName = "x", EmergencyContactPhone = "y", EmergencyContactRelationship = "z",
                IsStoredValueCustomer = storedValue, StoredValueBalance = balance,
            });
            var pet = await sp.GetRequiredService<PetService>().CreateAsync(new PetInput
            {
                OwnerId = owner.OwnerId, Name = "毛毛", Species = "犬", Breed = "柴犬",
                Gender = "公", Age = "3", IsNeutered = true,
                Personality = new() { "親人" }, MedicalHistory = new(),
                PhysicalExamination = new PhysicalExamination(),
            });
            var appt = await sp.GetRequiredService<AppointmentService>().CreateAsync(new AppointmentCreateInput
            {
                PetId = pet.PetId, Date = new DateOnly(2026, 6, 1), Time = new TimeOnly(10, 0),
            });
            return appt.AppointmentId;
        });
    }

    [Fact]
    public async Task LoadForAppointment_populates_header()
    {
        var apptId = await SetupAsync();
        var vm = CreateVm();
        vm.LoadForAppointment(apptId);
        for (int i = 0; i < 20 && string.IsNullOrEmpty(vm.OwnerName); i++) await Task.Delay(50);

        vm.OwnerName.Should().Be("張三");
        vm.PetName.Should().Be("毛毛");
        vm.ServiceDate.Should().Be(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public async Task LoadForAppointment_inherits_personality_from_pet_when_no_record()
    {
        var apptId = await SetupAsync();
        var vm = CreateVm();
        vm.LoadForAppointment(apptId);
        for (int i = 0; i < 20 && string.IsNullOrEmpty(vm.OwnerName); i++) await Task.Delay(50);

        vm.Personality.Single(p => p.Name == "親人").IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task Services_selection_changes_TotalCost()
    {
        var apptId = await SetupAsync();
        var vm = CreateVm();
        vm.LoadForAppointment(apptId);
        for (int i = 0; i < 20 && string.IsNullOrEmpty(vm.OwnerName); i++) await Task.Delay(50);

        vm.Services.Single(s => s.Name == "洗澡").IsSelected = true;
        vm.Services.Single(s => s.Name == "洗澡").Price = 500;
        vm.Services.Single(s => s.Name == "美容").IsSelected = true;
        vm.Services.Single(s => s.Name == "美容").Price = 800;

        vm.TotalCost.Should().Be(1300);
    }

    [Fact]
    public async Task StoredValue_deduction_computed_when_owner_is_storedValue_customer()
    {
        var apptId = await SetupAsync(storedValue: true, balance: 1000);
        var vm = CreateVm();
        vm.LoadForAppointment(apptId);
        for (int i = 0; i < 20 && !vm.IsStoredValueCustomer; i++) await Task.Delay(50);

        vm.IsStoredValueCustomer.Should().BeTrue();
        vm.StoredValueBalance.Should().Be(1000);

        vm.Services.Single(s => s.Name == "洗澡").IsSelected = true;
        vm.Services.Single(s => s.Name == "洗澡").Price = 600;

        vm.StoredValueDeduction.Should().Be(600);
        vm.CashPayment.Should().Be(0);
        vm.StoredValueRemaining.Should().Be(400);
    }

    [Fact]
    public async Task Save_persists_and_shows_success()
    {
        var apptId = await SetupAsync();
        var vm = CreateVm();
        vm.LoadForAppointment(apptId);
        for (int i = 0; i < 20 && string.IsNullOrEmpty(vm.OwnerName); i++) await Task.Delay(50);

        vm.Services.Single(s => s.Name == "洗澡").IsSelected = true;
        vm.Services.Single(s => s.Name == "洗澡").Price = 500;

        await vm.SaveCommand.ExecuteAsync(null);
        _dialog.Successes.Should().ContainSingle();
        vm.GroomingRecordId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateContract_fails_without_saved_record()
    {
        var apptId = await SetupAsync();
        var vm = CreateVm();
        vm.LoadForAppointment(apptId);
        for (int i = 0; i < 20 && string.IsNullOrEmpty(vm.OwnerName); i++) await Task.Delay(50);

        // 尚未儲存：GroomingRecordId 為 null
        vm.GroomingRecordId.Should().BeNullOrEmpty();
        vm.GenerateContractCommand.Execute(null);

        _dialog.Errors.Should().ContainSingle().Which.title.Should().Be("尚未儲存");
    }
}
