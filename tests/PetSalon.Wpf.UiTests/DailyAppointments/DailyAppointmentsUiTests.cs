using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.DailyAppointments;

[Trait("category", "ui")]
[Collection("uiseq")]
public class DailyAppointmentsUiTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;
    public DailyAppointmentsUiTests(AppFixture fx)
    {
        _fx = fx;
        // 進入「本日預約」: 從首頁的 CTA「查看今日預約」
        _fx.ClickById("nav-home");
        _fx.ClickById("btn-home-go-today");
    }

    [Fact]
    public void Daily_page_shows_action_buttons()
    {
        _fx.WaitForId("btn-mark-complete").Should().NotBeNull();
        _fx.WaitForId("btn-cancel-appointment").Should().NotBeNull();
        _fx.WaitForId("btn-delete-appointment").Should().NotBeNull();
        _fx.WaitForId("txt-cancel-reason").Should().NotBeNull();
    }

    [Fact]
    public void MarkComplete_without_selection_shows_error_dialog()
    {
        _fx.ClickById("btn-mark-complete");
        var dlg = _fx.FindDialog("尚未選取");
        dlg.Should().NotBeNull();
        _fx.CloseAllDialogs();
    }

    [Fact]
    public void Cancel_appointment_without_selection_shows_error()
    {
        _fx.ClickById("btn-cancel-appointment");
        var dlg = _fx.FindDialog("尚未選取");
        dlg.Should().NotBeNull();
        _fx.CloseAllDialogs();
    }

    [Fact]
    public void Delete_appointment_without_selection_shows_error()
    {
        _fx.ClickById("btn-delete-appointment");
        var dlg = _fx.FindDialog("尚未選取");
        dlg.Should().NotBeNull();
        _fx.CloseAllDialogs();
    }

    [Fact]
    public void Back_button_returns_to_calendar()
    {
        var back = _fx.MainWindow.FindFirstDescendant(c => c.ByText("← 返回日曆"))!.AsButton();
        back.Invoke();
        Thread.Sleep(300);
        // 回到行事曆應該找得到 月/週/日 切換按鈕
        var month = _fx.MainWindow.FindFirstDescendant(c => c.ByText("月"));
        month.Should().NotBeNull();
    }
}
