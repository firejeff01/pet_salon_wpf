using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.HomePage;

[Trait("category", "ui")]
[Collection("uiseq")]
public class HomePageUiTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;
    public HomePageUiTests(AppFixture fx) { _fx = fx; }

    [Fact]
    public void Home_shows_hero_with_welcome()
    {
        // 啟動後預設 Home
        var welcome = _fx.MainWindow.FindFirstDescendant(c => c.ByText("歡迎回來 🐾"));
        welcome.Should().NotBeNull();
    }

    [Fact]
    public void Home_shows_two_CTAs()
    {
        _fx.WaitForId("btn-home-go-today").Should().NotBeNull();
        _fx.WaitForId("btn-home-go-owners").Should().NotBeNull();
    }

    [Fact]
    public void Click_GoOwners_navigates_to_OwnerPage()
    {
        _fx.ClickById("btn-home-go-owners");
        _fx.WaitForId("btn-new-owner").Should().NotBeNull();
        // 切回首頁
        _fx.ClickById("nav-home");
    }

    [Fact]
    public void Click_GoToday_navigates_to_daily_list()
    {
        _fx.ClickById("btn-home-go-today");
        // 進到當日清單 → 應該找得到「← 返回日曆」按鈕（沒有 AutomationId 但有文字）
        var back = _fx.MainWindow.FindFirstDescendant(c => c.ByText("← 返回日曆"));
        back.Should().NotBeNull();
        _fx.ClickById("nav-home");
    }

    [Fact]
    public void Home_counts_visible_for_owner_pet_today()
    {
        _fx.ClickById("nav-home");
        // 4 個功能卡（飼主管理、預約日曆、客戶填寫、備份管理）至少其一存在
        var anyCard = _fx.MainWindow.FindFirstDescendant(c => c.ByText("飼主管理"))
                   ?? _fx.MainWindow.FindAnyContaining("目前共有")
                   ?? _fx.MainWindow.FindAnyContaining("今天有");
        anyCard.Should().NotBeNull();
    }
}
