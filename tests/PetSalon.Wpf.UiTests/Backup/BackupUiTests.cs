using System.IO;
using FlaUI.Core.AutomationElements;
using FluentAssertions;
using PetSalon.Wpf.UiTests.Helpers;
using Xunit;

namespace PetSalon.Wpf.UiTests.Backup;

[Trait("category", "ui")]
[Collection("uiseq")]
public class BackupUiTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _fx;
    public BackupUiTests(AppFixture fx)
    {
        _fx = fx;
        _fx.ClickById("nav-backup");
    }

    [Fact]
    public void Backup_page_renders_grid_and_buttons()
    {
        _fx.WaitForId("btn-create-backup").Should().NotBeNull();
        _fx.WaitForId("btn-restore-backup").Should().NotBeNull();
        _fx.WaitForId("btn-delete-backup").Should().NotBeNull();
        _fx.WaitForId("grid-backups").Should().NotBeNull();
    }

    [Fact]
    public void Create_backup_creates_zip_on_disk()
    {
        // MessageDialog（WindowStyle=None）會卡住 UI 執行緒讓 RefreshAsync 無法跑
        // → 改驗檔案系統副作用（更接近真實業務行為，UI 顯示由 VM 測試保證）
        var backupDir = Path.Combine(_fx.AppDataDir, "backups");
        Directory.CreateDirectory(backupDir);
        var before = Directory.GetFiles(backupDir, "*.zip").Length;

        _fx.ClickById("btn-create-backup");
        Thread.Sleep(2500);    // 等 backup 落地 + dialog 顯示
        _fx.CloseAllDialogs(); // 收尾關 dialog（不影響本斷言）

        Directory.GetFiles(backupDir, "*.zip").Length.Should().Be(before + 1);
    }

    [Fact]
    public void Restore_without_selection_shows_error_dialog()
    {
        _fx.ClickById("btn-restore-backup");
        var dlg = _fx.FindDialog("尚未選取");
        dlg.Should().NotBeNull();
        _fx.CloseAllDialogs();
    }
}
