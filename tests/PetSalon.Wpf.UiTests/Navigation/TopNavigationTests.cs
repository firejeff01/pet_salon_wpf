using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.Navigation;

[Trait("category", "ui")]
[Collection("uiseq")]
public class TopNavigationTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;
    public TopNavigationTests(AppFixture fx) { _fx = fx; }

    [Fact]
    public void App_launches_and_shows_5_nav_buttons()
    {
        _fx.WaitForId("nav-home").Should().NotBeNull();
        _fx.WaitForId("nav-owners").Should().NotBeNull();
        _fx.WaitForId("nav-calendar").Should().NotBeNull();
        _fx.WaitForId("nav-customer").Should().NotBeNull();
        _fx.WaitForId("nav-backup").Should().NotBeNull();
    }

    [Fact]
    public void Navigating_to_each_top_level_page_does_not_crash()
    {
        foreach (var id in new[] { "nav-owners", "nav-calendar", "nav-customer", "nav-backup", "nav-home" })
        {
            _fx.ClickById(id);
            _fx.MainWindow.Properties.IsOffscreen.Value.Should().BeFalse();
        }
    }

    [Fact]
    public void Window_title_contains_brand()
    {
        _fx.MainWindow.Title.Should().Contain("貳寶");
    }
}
