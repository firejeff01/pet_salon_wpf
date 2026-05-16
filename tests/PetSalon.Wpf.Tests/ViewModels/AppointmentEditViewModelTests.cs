using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

public class AppointmentEditViewModelTests : VmTestBase
{
    private readonly FakeDialogService _dialog = new();
    private AppointmentEditViewModel CreateVm() => new(ScopeFactory, _dialog);

    private async Task<string> CreatePetAsync()
    {
        return await InScopeAsync(async sp =>
        {
            var owner = await sp.GetRequiredService<OwnerService>().CreateAsync(new OwnerInput
            {
                Name = "張三", NationalId = "A1", Phone = "0912", Address = "桃園",
                EmergencyContactName = "x", EmergencyContactPhone = "y", EmergencyContactRelationship = "z",
            });
            var pet = await sp.GetRequiredService<PetService>().CreateAsync(new PetInput
            {
                OwnerId = owner.OwnerId, Name = "毛毛", Species = "犬", Breed = "柴犬",
                Gender = "公", Age = "3", IsNeutered = true,
                Personality = new() { "親人" }, MedicalHistory = new(),
                PhysicalExamination = new PhysicalExamination(),
            });
            return pet.PetId;
        });
    }

    [Fact]
    public async Task LoadForCreate_sets_defaults()
    {
        var petId = await CreatePetAsync();
        var vm = CreateVm();
        await vm.LoadForCreateAsync(new DateOnly(2026, 7, 1), petId);

        vm.AppointmentId.Should().BeNull();
        vm.Date.Should().Be(new DateOnly(2026, 7, 1));
        vm.PetId.Should().Be(petId);
        vm.TimeText.Should().Be("10:00");
    }

    [Fact]
    public async Task Save_valid_input_creates_appointment_and_shows_success()
    {
        var petId = await CreatePetAsync();
        var vm = CreateVm();
        await vm.LoadForCreateAsync(new DateOnly(2026, 7, 1), petId);
        vm.TimeText = "14:30";
        vm.Note = "首次美容";

        bool? closeResult = null;
        vm.RequestClose += r => closeResult = r;

        await vm.SaveCommand.ExecuteAsync(null);

        closeResult.Should().BeTrue();
        _dialog.Successes.Should().ContainSingle();
        _dialog.Successes[0].message.Should().Contain("已建立");
    }

    [Fact]
    public async Task Save_invalid_time_format_fails()
    {
        var petId = await CreatePetAsync();
        var vm = CreateVm();
        await vm.LoadForCreateAsync(new DateOnly(2026, 7, 1), petId);
        vm.TimeText = "25:99";  // 無效時間

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Contain("時間");
        _dialog.Successes.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_raises_close_false()
    {
        var vm = CreateVm();
        bool? r = null;
        vm.RequestClose += x => r = x;
        vm.CancelCommand.Execute(null);
        r.Should().BeFalse();
    }

    // ============ TDD #3: 加飼主 + 寵物下拉 ============

    [Fact]
    public async Task LoadForCreate_no_pet_loads_all_owners()
    {
        // 建兩位飼主
        await InScopeAsync<int>(async sp =>
        {
            await sp.GetRequiredService<OwnerService>().CreateAsync(new OwnerInput
            {
                Name = "A", NationalId = "1", Phone = "a", Address = "addr",
                EmergencyContactName = "e", EmergencyContactPhone = "p", EmergencyContactRelationship = "r",
            });
            await sp.GetRequiredService<OwnerService>().CreateAsync(new OwnerInput
            {
                Name = "B", NationalId = "2", Phone = "b", Address = "addr",
                EmergencyContactName = "e", EmergencyContactPhone = "p", EmergencyContactRelationship = "r",
            });
            return 0;
        });
        var vm = CreateVm();
        await vm.LoadForCreateAsync(new DateOnly(2026, 7, 1));

        vm.AvailableOwners.Should().HaveCount(2);
    }

    [Fact]
    public async Task Selecting_owner_loads_only_that_owner_pets()
    {
        // 飼主 A 有寵物毛毛，飼主 B 有寵物汪汪
        var (ownerA, ownerB) = await InScopeAsync(async sp =>
        {
            var a = await sp.GetRequiredService<OwnerService>().CreateAsync(new OwnerInput
            {
                Name = "A", NationalId = "1", Phone = "a", Address = "addr",
                EmergencyContactName = "e", EmergencyContactPhone = "p", EmergencyContactRelationship = "r",
            });
            var b = await sp.GetRequiredService<OwnerService>().CreateAsync(new OwnerInput
            {
                Name = "B", NationalId = "2", Phone = "b", Address = "addr",
                EmergencyContactName = "e", EmergencyContactPhone = "p", EmergencyContactRelationship = "r",
            });
            await sp.GetRequiredService<PetService>().CreateAsync(new PetInput
            {
                OwnerId = a.OwnerId, Name = "毛毛", Species = "犬", Breed = "x",
                Gender = "公", Age = "1", IsNeutered = false,
                Personality = new() { "親人" }, MedicalHistory = new(),
                PhysicalExamination = new PhysicalExamination(),
            });
            await sp.GetRequiredService<PetService>().CreateAsync(new PetInput
            {
                OwnerId = b.OwnerId, Name = "汪汪", Species = "犬", Breed = "x",
                Gender = "公", Age = "1", IsNeutered = false,
                Personality = new() { "親人" }, MedicalHistory = new(),
                PhysicalExamination = new PhysicalExamination(),
            });
            return (a.OwnerId, b.OwnerId);
        });

        var vm = CreateVm();
        await vm.LoadForCreateAsync(new DateOnly(2026, 7, 1));

        vm.SelectedOwnerId = ownerA;
        // 等 partial OnSelectedOwnerIdChanged 觸發
        for (int i = 0; i < 20 && vm.AvailablePets.Count == 0; i++) await Task.Delay(50);
        vm.AvailablePets.Should().ContainSingle().Which.Name.Should().Be("毛毛");

        vm.SelectedOwnerId = ownerB;
        for (int i = 0; i < 20 && (vm.AvailablePets.Count != 1 || vm.AvailablePets[0].Name != "汪汪"); i++) await Task.Delay(50);
        vm.AvailablePets.Should().ContainSingle().Which.Name.Should().Be("汪汪");
    }

    [Fact]
    public async Task LoadForEdit_pre_selects_owner_and_pet()
    {
        var petId = await CreatePetAsync();
        var apptId = await InScopeAsync<string>(async sp =>
        {
            var appt = await sp.GetRequiredService<AppointmentService>().CreateAsync(new AppointmentCreateInput
            {
                PetId = petId, Date = new DateOnly(2026, 7, 1), Time = new TimeOnly(10, 0),
            });
            return appt.AppointmentId;
        });

        var vm = CreateVm();
        await vm.LoadForEditAsync(apptId);
        for (int i = 0; i < 20 && string.IsNullOrEmpty(vm.SelectedOwnerId); i++) await Task.Delay(50);

        vm.SelectedOwnerId.Should().NotBeNullOrEmpty();
        vm.PetId.Should().Be(petId);
    }
}
