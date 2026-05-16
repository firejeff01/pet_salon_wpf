using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Services;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

public class CustomerFormViewModelTests : VmTestBase
{
    private readonly FakeDialogService _dialog = new();
    private CustomerFormViewModel CreateVm() => new(ScopeFactory, _dialog);

    private static void FillOwner(CustomerFormViewModel vm, string name = "新客戶")
    {
        vm.Owner.Name = name; vm.Owner.NationalId = "X1"; vm.Owner.Phone = "0912";
        vm.Owner.Address = "桃園"; vm.Owner.EmergencyContactName = "聯絡人";
        vm.Owner.EmergencyContactPhone = "0987"; vm.Owner.EmergencyContactRelationship = "配偶";
    }

    private static void FillPet(CustomerPetEntry pet, string name)
    {
        pet.Name = name; pet.Breed = "x"; pet.Age = "2"; pet.Species = "犬"; pet.Gender = "公";
    }

    [Fact]
    public void Default_starts_with_one_pet_entry()
    {
        var vm = CreateVm();
        vm.Pets.Should().HaveCount(1);
    }

    [Fact]
    public void AddPetEntry_appends_new_entry()
    {
        var vm = CreateVm();
        vm.AddPetEntryCommand.Execute(null);
        vm.Pets.Should().HaveCount(2);
    }

    [Fact]
    public void RemovePetEntry_removes_specified()
    {
        var vm = CreateVm();
        vm.AddPetEntryCommand.Execute(null);
        var second = vm.Pets[1];

        vm.RemovePetEntryCommand.Execute(second);

        vm.Pets.Should().ContainSingle();
    }

    [Fact]
    public void RemovePetEntry_keeps_at_least_one_entry()
    {
        var vm = CreateVm();
        var only = vm.Pets[0];
        vm.RemovePetEntryCommand.Execute(only);
        vm.Pets.Should().ContainSingle();   // 不能砍光
    }

    [Fact]
    public async Task Submit_owner_only_creates_owner_no_pets_when_pet_names_empty()
    {
        var vm = CreateVm();
        FillOwner(vm);

        await vm.SubmitCommand.ExecuteAsync(null);

        _dialog.Successes.Should().ContainSingle();
        var pets = await InScopeAsync(sp => sp.GetRequiredService<PetService>().ListAsync());
        pets.Should().BeEmpty();
    }

    [Fact]
    public async Task Submit_with_single_pet_creates_both()
    {
        var vm = CreateVm();
        FillOwner(vm);
        FillPet(vm.Pets[0], "毛毛");

        await vm.SubmitCommand.ExecuteAsync(null);

        var pets = await InScopeAsync(sp => sp.GetRequiredService<PetService>().ListAsync());
        pets.Should().ContainSingle().Which.Name.Should().Be("毛毛");
    }

    [Fact]
    public async Task Submit_with_multiple_pets_creates_all_for_same_owner()
    {
        var vm = CreateVm();
        FillOwner(vm);
        FillPet(vm.Pets[0], "毛毛");
        vm.AddPetEntryCommand.Execute(null);
        FillPet(vm.Pets[1], "汪汪");
        vm.AddPetEntryCommand.Execute(null);
        FillPet(vm.Pets[2], "咪咪");

        await vm.SubmitCommand.ExecuteAsync(null);

        var pets = await InScopeAsync(sp => sp.GetRequiredService<PetService>().ListAsync());
        pets.Should().HaveCount(3);
        pets.Select(p => p.Name).Should().BeEquivalentTo(new[] { "毛毛", "汪汪", "咪咪" });
        pets.Select(p => p.OwnerId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Submit_validation_failure_no_success()
    {
        var vm = CreateVm();
        await vm.SubmitCommand.ExecuteAsync(null);
        _dialog.Successes.Should().BeEmpty();
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task After_successful_submit_form_resets_to_single_empty_pet()
    {
        var vm = CreateVm();
        FillOwner(vm);
        FillPet(vm.Pets[0], "毛毛");
        vm.AddPetEntryCommand.Execute(null);
        FillPet(vm.Pets[1], "汪汪");

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.Owner.Name.Should().BeEmpty();
        vm.Pets.Should().ContainSingle();
        vm.Pets[0].Name.Should().BeEmpty();
    }

    [Fact]
    public void Reset_clears_to_single_empty()
    {
        var vm = CreateVm();
        FillOwner(vm);
        FillPet(vm.Pets[0], "毛毛");
        vm.AddPetEntryCommand.Execute(null);

        vm.ResetCommand.Execute(null);

        vm.Owner.Name.Should().BeEmpty();
        vm.Pets.Should().ContainSingle();
        vm.Pets[0].Name.Should().BeEmpty();
    }
}
