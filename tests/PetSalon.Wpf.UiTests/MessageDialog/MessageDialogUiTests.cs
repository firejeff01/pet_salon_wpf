using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.MessageDialog;

[Trait("category", "ui")]
[Collection("uiseq")]
public class MessageDialogUiTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;
    public MessageDialogUiTests(AppFixture fx) { _fx = fx; }

    [Fact]
    public void Info_dialog_opens_and_closes_via_OK()
    {
        // 開啟「關於」 dialog（在 Menu）
        var help = _fx.MainWindow.FindFirstDescendant(c => c.ByText("說明(_H)"))!.AsMenuItem();
        help.Click();
        Thread.Sleep(200);
        var about = _fx.MainWindow.FindFirstDescendant(c => c.ByText("關於"))!.AsMenuItem();
        about.Click();
        Thread.Sleep(300);

        var dlg = _fx.FindDialog("關於");
        dlg.Should().NotBeNull();
        var ok = dlg!.FindFirstDescendant(c => c.ByText("確定"))!.AsButton();
        ok.Invoke();
        Thread.Sleep(300);
    }

    [Fact]
    public void Error_dialog_renders_with_red_header()
    {
        // 透過 Backup 還原（未選取）→ 觸發 Error dialog
        _fx.ClickById("nav-backup");
        _fx.ClickById("btn-restore-backup");
        Thread.Sleep(300);

        var dlg = _fx.FindDialog("尚未選取");
        dlg.Should().NotBeNull("Error 對話框未出現");
        var ok = dlg!.FindFirstDescendant(c => c.ByText("確定"))!.AsButton();
        ok.Invoke();
    }
}
