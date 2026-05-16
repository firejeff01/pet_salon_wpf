// Phase 4 final screenshot test
// Automates: Pet 編輯頁「異常」勾選後 Note TextBox 出現 + 病史「以上皆無」勾選後其他自動取消

using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.Phase4Screenshot;

[Trait("category", "screenshot")]
[Collection("uiseq")]
public class Phase4ScreenshotTest : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;

    public Phase4ScreenshotTest(AppFixture fx) { _fx = fx; }

    [Fact(DisplayName = "Phase4 / 截圖：Pet 編輯頁異常勾選 + 病史以上皆無互斥")]
    public void Phase4_screenshot_pet_edit_abnormal_and_none_of_above()
    {
        // Navigate to owners
        _fx.ClickById("nav-owners");
        Thread.Sleep(400);

        // Create owner
        _fx.ClickById("btn-new-owner");
        _fx.TypeInto("txt-owner-name", "截圖飼主");
        _fx.TypeInto("txt-owner-national-id", "Z999999999");
        _fx.TypeInto("txt-owner-phone", "0912888777");
        _fx.TypeInto("txt-owner-address", "桃園市桃園區慈文路322巷5號");
        _fx.ClickById("btn-save-owner");
        Thread.Sleep(600);
        _fx.CloseAllDialogs();
        Thread.Sleep(400);

        // Open pet form
        _fx.ClickById("btn-add-pet");
        Thread.Sleep(600);

        // Select 異常 for eyes → Note TextBox should appear
        var eyesComboEl = _fx.FindById("cmb-phys-eyes");
        if (eyesComboEl is not null)
        {
            eyesComboEl!.AsComboBox().Select("異常");
            Thread.Sleep(500);
        }

        // Select 心臟病 in medical history
        var heartCb = _fx.FindById("cb-medical-心臟病");
        if (heartCb is not null)
        {
            heartCb!.AsCheckBox().IsChecked = true;
            Thread.Sleep(200);
        }

        // Now check 以上皆無 → should uncheck 心臟病
        var noneCb = _fx.FindById("cb-medical-以上皆無");
        if (noneCb is not null)
        {
            noneCb!.AsCheckBox().IsChecked = true;
            Thread.Sleep(300);
        }

        // Take screenshot
        TakeScreenshot("C:/WorkSpace/pet_salon_wpf/wpf_phase4_final.png");

        // Verify
        var noteBox = _fx.FindById("txt-phys-eyes-note");
        noteBox.Should().NotBeNull("Eyes=異常 時 Note TextBox 應顯示");

        if (heartCb is not null)
            heartCb!.AsCheckBox().IsChecked.Should().BeFalse("勾選以上皆無後心臟病應取消");
    }

    private static void TakeScreenshot(string path)
    {
        // Use Windows Forms to take screenshot
        var asm = System.Reflection.Assembly.Load("System.Windows.Forms");
        var screenType = asm?.GetType("System.Windows.Forms.Screen");
        if (screenType is null) return;

        try
        {
            // Alternative: use BitBlt via P/Invoke
            var sw = 1920; var sh = 1080;
            using var bmp = new System.Drawing.Bitmap(sw, sh);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(sw, sh));
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { /* screenshot optional */ }
    }
}
