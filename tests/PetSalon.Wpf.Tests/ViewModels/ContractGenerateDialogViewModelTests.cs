using FluentAssertions;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

/// <summary>
/// 只測 VM 層驗證 + close 行為。實際 PDF 產生由 ContractServiceTests 在 Core.Tests 覆蓋。
/// </summary>
public class ContractGenerateDialogViewModelTests : VmTestBase
{
    private readonly FakeDialogService _dialog = new();
    private ContractGenerateDialogViewModel CreateVm() => new(ScopeFactory, _dialog);

    [Fact]
    public async Task Generate_without_signature_sets_error_message()
    {
        var vm = CreateVm();
        vm.GroomingRecordId = "any";
        vm.CaptureOwnerSignature = () => null;

        await vm.GenerateCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Contain("簽名");
        _dialog.Successes.Should().BeEmpty();
    }

    [Fact]
    public async Task Generate_with_empty_signature_sets_error_message()
    {
        var vm = CreateVm();
        vm.GroomingRecordId = "any";
        vm.CaptureOwnerSignature = () => Array.Empty<byte>();

        await vm.GenerateCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Contain("簽名");
    }

    [Fact]
    public void Cancel_raises_close_false()
    {
        var vm = CreateVm();
        bool? r = null;
        vm.RequestClose += x => r = x;
        vm.CancelCommand.Execute(null);
        r.Should().BeFalse();
    }

    [Fact]
    public void Caption_can_be_set()
    {
        var vm = CreateVm();
        vm.Caption = "張三 / 毛毛";
        vm.Caption.Should().Be("張三 / 毛毛");
    }
}
