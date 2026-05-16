using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.Calendar;

[Trait("category", "ui")]
[Collection("uiseq")]
public class CalendarUiTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;
    public CalendarUiTests(AppFixture fx)
    {
        _fx = fx;
        _fx.ClickById("nav-calendar");
    }

    [Fact]
    public void Calendar_renders_month_label()
    {
        var label = _fx.WaitForId("txt-cal-month-label");
        label.Should().NotBeNull();
        var text = label.Properties.Name.ValueOrDefault ?? string.Empty;
        text.Should().Contain("年").And.Contain("月");
    }

    [Fact]
    public void Switch_view_buttons_visible()
    {
        _fx.WaitForId("btn-view-month").Should().NotBeNull();
        _fx.WaitForId("btn-view-week").Should().NotBeNull();
        _fx.WaitForId("btn-view-day").Should().NotBeNull();
        _fx.WaitForId("btn-view-today").Should().NotBeNull();
    }

    [Fact]
    public void Today_button_click_does_not_crash()
    {
        _fx.ClickById("btn-view-today");
        _fx.MainWindow.Properties.IsOffscreen.Value.Should().BeFalse();
    }

    [Fact]
    public void Prev_and_next_month_change_label()
    {
        var before = _fx.WaitForId("txt-cal-month-label").Properties.Name.ValueOrDefault ?? string.Empty;
        _fx.ClickById("btn-cal-prev");
        Thread.Sleep(200);
        var after = _fx.WaitForId("txt-cal-month-label").Properties.Name.ValueOrDefault ?? string.Empty;
        after.Should().NotBe(before);
        _fx.ClickById("btn-cal-next");
    }

    [Fact]
    public void Switch_to_week_view_then_back_to_month()
    {
        _fx.ClickById("btn-view-week");
        Thread.Sleep(200);
        _fx.ClickById("btn-view-month");
        Thread.Sleep(200);
        _fx.MainWindow.Properties.IsOffscreen.Value.Should().BeFalse();
    }
}
