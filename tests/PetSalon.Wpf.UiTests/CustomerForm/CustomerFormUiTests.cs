using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.CustomerForm;

[Trait("category", "ui")]
[Collection("uiseq")]
public class CustomerFormUiTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;
    public CustomerFormUiTests(AppFixture fx)
    {
        _fx = fx;
        _fx.ClickById("nav-customer");
    }

    [Fact]
    public void Customer_form_renders_with_submit_and_reset_buttons()
    {
        _fx.WaitForId("btn-customer-submit").Should().NotBeNull();
        _fx.WaitForId("btn-customer-reset").Should().NotBeNull();
        _fx.WaitForId("btn-customer-add-pet").Should().NotBeNull();
    }

    [Fact]
    public void Submit_empty_form_does_not_show_success()
    {
        _fx.ClickById("btn-customer-submit");
        Thread.Sleep(500);
        var success = _fx.MainWindow.ModalWindows.FirstOrDefault(w => w.Title?.Contains("已送出") == true);
        success.Should().BeNull();
        _fx.CloseAllDialogs();
    }

    [Fact]
    public void AddPet_button_adds_one_more_pet_card()
    {
        // 初始 1 隻：找 "🐾 寵物" 文字數量
        var initial = _fx.MainWindow.FindAllDescendants(c => c.ByText("🐾 寵物")).Length;
        _fx.ClickById("btn-customer-add-pet");
        Thread.Sleep(300);
        var after = _fx.MainWindow.FindAllDescendants(c => c.ByText("🐾 寵物")).Length;
        after.Should().Be(initial + 1);
    }
}

[CollectionDefinition("uiseq", DisableParallelization = true)]
public class UiSeqCollection { }
