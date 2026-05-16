using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.OwnerPage;

[Trait("category", "ui")]
[Collection("uiseq")]
public class OwnerPageUiTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;
    public OwnerPageUiTests(AppFixture fx)
    {
        _fx = fx;
        _fx.ClickById("nav-owners");
    }

    private void FillOwnerForm(string suffix)
    {
        _fx.TypeInto("txt-owner-name", "張三-" + suffix);
        _fx.TypeInto("txt-owner-national-id", "A1" + suffix);
        _fx.TypeInto("txt-owner-phone", "0912-" + suffix);
        _fx.TypeInto("txt-owner-address", "桃園");
        _fx.TypeInto("txt-owner-emergency-name", "李四");
        _fx.TypeInto("txt-owner-emergency-phone", "0987");
        _fx.TypeInto("txt-owner-emergency-relationship", "配偶");
    }

    [Fact]
    public void NewOwner_button_clears_form()
    {
        _fx.ClickById("btn-new-owner");
        var name = _fx.WaitForId("txt-owner-name").AsTextBox().Text;
        name.Should().BeEmpty();
    }

    [Fact]
    public void Save_with_required_fields_shows_success_dialog()
    {
        _fx.ClickById("btn-new-owner");
        FillOwnerForm("save-test");
        _fx.ClickById("btn-save-owner");

        var dlg = _fx.FindDialog("儲存成功");
        dlg.Should().NotBeNull("儲存成功對話框應該要出現");
        _fx.CloseAllDialogs();
    }

    [Fact]
    public void Save_with_empty_form_shows_validation_error_inline()
    {
        _fx.ClickById("btn-new-owner");
        _fx.ClickById("btn-save-owner");

        // 應該不會跳成功 dialog
        Thread.Sleep(500);
        var success = _fx.MainWindow.ModalWindows.FirstOrDefault(w => w.Title?.Contains("儲存成功") == true);
        success.Should().BeNull();
    }

    [Fact]
    public void AddPet_button_disabled_when_no_owner_selected()
    {
        _fx.ClickById("btn-new-owner");
        var btn = _fx.WaitForId("btn-add-pet");
        btn.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void AddAppointment_button_disabled_when_no_owner_selected()
    {
        _fx.ClickById("btn-new-owner");
        var btn = _fx.WaitForId("btn-add-appointment");
        btn.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void After_creating_owner_AddPet_becomes_enabled()
    {
        _fx.ClickById("btn-new-owner");
        FillOwnerForm("addpet-ena");
        _fx.ClickById("btn-save-owner");
        _fx.CloseAllDialogs();

        // 儲存成功後 SelectedOwner 應被設為剛建立的飼主
        Thread.Sleep(300);
        var btn = _fx.WaitForId("btn-add-pet");
        btn.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Created_owner_appears_in_list()
    {
        _fx.ClickById("btn-new-owner");
        FillOwnerForm("appear-test");
        _fx.ClickById("btn-save-owner");
        _fx.CloseAllDialogs();

        // 在 ListBox 找這位飼主
        Thread.Sleep(300);
        var hit = _fx.MainWindow.FindFirstDescendant(c => c.ByText("張三-appear-test"));
        hit.Should().NotBeNull();
    }
}
