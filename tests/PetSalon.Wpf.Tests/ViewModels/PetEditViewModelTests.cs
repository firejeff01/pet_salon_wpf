using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

public class PetEditViewModelTests : VmTestBase
{
    private readonly FakeDialogService _dialog = new();
    private PetEditViewModel CreateVm() => new(ScopeFactory, _dialog);

    private async Task<string> CreateOwnerAsync()
    {
        return await InScopeAsync(async sp =>
        {
            var owner = await sp.GetRequiredService<OwnerService>().CreateAsync(new OwnerInput
            {
                Name = "張三", NationalId = "A1", Phone = "0912",
                Address = "桃園", EmergencyContactName = "聯絡人",
                EmergencyContactPhone = "0987", EmergencyContactRelationship = "配偶",
            });
            return owner.OwnerId;
        });
    }

    [Fact]
    public async Task LoadForCreate_resets_form_to_defaults()
    {
        var ownerId = await CreateOwnerAsync();
        var vm = CreateVm();
        await vm.LoadForCreateAsync(ownerId);

        vm.PetId.Should().BeNull();
        vm.OwnerId.Should().Be(ownerId);
        vm.Species.Should().Be("犬");
        vm.Gender.Should().Be("公");
        vm.IsCreating.Should().BeTrue();
        vm.Personality.All(p => !p.IsSelected).Should().BeTrue();
    }

    [Fact]
    public async Task LoadForEdit_populates_form_from_existing_pet()
    {
        var ownerId = await CreateOwnerAsync();
        var petId = await InScopeAsync(async sp =>
        {
            var p = await sp.GetRequiredService<PetService>().CreateAsync(new PetInput
            {
                OwnerId = ownerId, Name = "毛毛", Species = "貓", Breed = "波斯",
                Gender = "母", Age = "5", IsNeutered = true, ChipNumber = "ABC123",
                Personality = new() { "親人", "親狗" }, MedicalHistory = new() { "心臟病" },
                PhysicalExamination = new PhysicalExamination { Fur = "打結" },
            });
            return p.PetId;
        });

        var vm = CreateVm();
        await vm.LoadForEditAsync(petId);

        vm.PetId.Should().Be(petId);
        vm.Name.Should().Be("毛毛");
        vm.Species.Should().Be("貓");
        vm.ChipNumber.Should().Be("ABC123");
        vm.Personality.Where(p => p.IsSelected).Select(p => p.Name).Should().BeEquivalentTo(new[] { "親人", "親狗" });
        vm.MedicalHistory.Where(m => m.IsSelected).Should().ContainSingle();
        vm.FurCondition.Should().Be("打結");
    }

    [Fact]
    public async Task Save_create_raises_RequestClose_true_and_shows_success()
    {
        var ownerId = await CreateOwnerAsync();
        var vm = CreateVm();
        await vm.LoadForCreateAsync(ownerId);
        vm.Name = "新寵物"; vm.Breed = "雪納瑞"; vm.Age = "2";
        vm.Personality[0].IsSelected = true;

        bool? closeResult = null;
        vm.RequestClose += r => closeResult = r;

        await vm.SaveCommand.ExecuteAsync(null);

        closeResult.Should().BeTrue();
        _dialog.Successes.Should().ContainSingle().Which.message.Should().Contain("新寵物").And.Contain("已建立");
    }

    [Fact]
    public async Task Save_validation_failure_keeps_dialog_open()
    {
        var ownerId = await CreateOwnerAsync();
        var vm = CreateVm();
        await vm.LoadForCreateAsync(ownerId);
        // 不填名稱 → 驗證失敗
        bool? closeResult = null;
        vm.RequestClose += r => closeResult = r;

        await vm.SaveCommand.ExecuteAsync(null);

        closeResult.Should().BeNull();
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        _dialog.Successes.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_raises_RequestClose_false()
    {
        var vm = CreateVm();
        bool? closeResult = null;
        vm.RequestClose += r => closeResult = r;

        vm.CancelCommand.Execute(null);

        closeResult.Should().BeFalse();
    }

    [Fact]
    public void SpeciesList_and_GenderList_match_domain_options()
    {
        var vm = CreateVm();
        vm.SpeciesList.Should().BeEquivalentTo(new[] { "犬", "貓" });
        vm.GenderList.Should().BeEquivalentTo(new[] { "公", "母" });
    }
}
