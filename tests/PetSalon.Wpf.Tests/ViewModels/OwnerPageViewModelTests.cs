using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

public class OwnerPageViewModelTests : VmTestBase
{
    private readonly FakeDialogService _dialog = new();
    private OwnerPageViewModel CreateVm() => new(ScopeFactory, _dialog);

    private static void FillValid(OwnerFormFields f, string name = "張三")
    {
        f.Name = name; f.NationalId = "A1"; f.Phone = "0912";
        f.Address = "桃園"; f.EmergencyContactName = "聯絡人";
        f.EmergencyContactPhone = "0987"; f.EmergencyContactRelationship = "配偶";
    }

    [Fact]
    public async Task InitializeAsync_loads_empty_when_db_empty()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.Owners.Should().BeEmpty();
    }

    [Fact]
    public async Task NewOwner_clears_form_and_marks_creating()
    {
        var vm = CreateVm();
        vm.NewOwnerCommand.Execute(null);
        vm.IsCreating.Should().BeTrue();
        vm.SelectedOwner.Should().BeNull();
        vm.Form.Name.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_create_owner_and_shows_success_dialog()
    {
        var vm = CreateVm();
        vm.NewOwnerCommand.Execute(null);
        FillValid(vm.Form, "新飼主");

        await vm.SaveCommand.ExecuteAsync(null);

        vm.Owners.Should().ContainSingle().Which.Name.Should().Be("新飼主");
        _dialog.Successes.Should().ContainSingle();
        _dialog.Successes[0].title.Should().Be("儲存成功");
        _dialog.Successes[0].message.Should().Contain("已建立");
        _dialog.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_update_existing_shows_success_with_update_msg()
    {
        var vm = CreateVm();
        // 先建立一筆
        vm.NewOwnerCommand.Execute(null);
        FillValid(vm.Form, "原名");
        await vm.SaveCommand.ExecuteAsync(null);
        _dialog.Successes.Clear();

        // 選取後改名再存
        vm.SelectedOwner = vm.Owners[0];
        vm.Form.Name = "新名";
        await vm.SaveCommand.ExecuteAsync(null);

        _dialog.Successes.Should().ContainSingle();
        _dialog.Successes[0].message.Should().Contain("已更新");
    }

    [Fact]
    public async Task Save_validation_failure_sets_error_message_no_success()
    {
        var vm = CreateVm();
        vm.NewOwnerCommand.Execute(null);
        // 不填任何欄位 → 必填驗證失敗
        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        _dialog.Successes.Should().BeEmpty();
    }

    [Fact]
    public async Task Selecting_owner_loads_form_and_pets()
    {
        var vm = CreateVm();
        vm.NewOwnerCommand.Execute(null);
        FillValid(vm.Form, "張三");
        await vm.SaveCommand.ExecuteAsync(null);

        // 切回未選 → 重新選取
        vm.SelectedOwner = null;
        vm.SelectedOwner = vm.Owners[0];

        vm.Form.Name.Should().Be("張三");
        vm.IsCreating.Should().BeFalse();
    }

    [Fact]
    public async Task AddPet_when_no_owner_shows_error_dialog()
    {
        var vm = CreateVm();
        vm.NewOwnerCommand.Execute(null);  // SelectedOwner 是 null
        vm.SelectedOwner.Should().BeNull();

        vm.AddPetCommand.Execute(null);

        _dialog.Errors.Should().ContainSingle().Which.title.Should().Be("尚未選取");
    }

    // ============ TDD #7: 飼主頁直接建預約 ============

    [Fact]
    public async Task AddAppointment_when_no_owner_shows_error()
    {
        var vm = CreateVm();
        vm.NewOwnerCommand.Execute(null);
        vm.AddAppointmentCommand.Execute(null);
        _dialog.Errors.Should().ContainSingle().Which.title.Should().Be("尚未選取");
    }

    [Fact]
    public async Task AddAppointment_when_owner_no_pets_shows_error()
    {
        var vm = CreateVm();
        vm.NewOwnerCommand.Execute(null);
        FillValid(vm.Form);
        await vm.SaveCommand.ExecuteAsync(null);
        _dialog.Errors.Clear();

        // 此飼主沒有寵物
        vm.AddAppointmentCommand.Execute(null);

        _dialog.Errors.Should().ContainSingle().Which.message.Should().Contain("寵物");
    }

    [Fact]
    public async Task AddAppointment_opens_dialog_with_owner_preselected()
    {
        var vm = CreateVm();
        vm.NewOwnerCommand.Execute(null);
        FillValid(vm.Form, "張三");
        await vm.SaveCommand.ExecuteAsync(null);
        var ownerId = vm.SelectedOwner!.OwnerId;

        await InScopeAsync<int>(async sp =>
        {
            await sp.GetRequiredService<PetService>().CreateAsync(new PetInput
            {
                OwnerId = ownerId, Name = "毛毛", Species = "犬", Breed = "x",
                Gender = "公", Age = "1", IsNeutered = false,
                Personality = new() { "親人" }, MedicalHistory = new(),
                PhysicalExamination = new PetSalon.Core.Entities.PhysicalExamination(),
            });
            return 0;
        });
        // VM cached OwnerPets — 重新選取以觸發 reload
        vm.SelectedOwner = null;
        vm.SelectedOwner = vm.Owners.Single(o => o.OwnerId == ownerId);
        for (int i = 0; i < 20 && vm.OwnerPets.Count == 0; i++) await Task.Delay(50);
        _dialog.Errors.Clear();

        vm.AddAppointmentCommand.Execute(null);

        _dialog.Dialogs.Should().ContainSingle().Which.title.Should().Be("新增預約");
        _dialog.Dialogs[0].vm.Should().BeOfType<AppointmentEditViewModel>();
    }
}
