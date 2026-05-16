using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class BackupPageViewModel : ViewModelBase
{
    private readonly IDialogService _dialog;

    public BackupPageViewModel(IServiceScopeFactory scope, IDialogService dialog) : base(scope)
    {
        _dialog = dialog;
    }

    public ObservableCollection<BackupFileInfo> Backups { get; } = new();
    [ObservableProperty] private BackupFileInfo? _selected;

    public override Task InitializeAsync() => RefreshAsync();

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    private Task RefreshAsync() => RunAsync(() =>
    {
        return WithScopeAsync(sp =>
        {
            var list = sp.GetRequiredService<BackupService>().List();
            Backups.Clear();
            foreach (var b in list) Backups.Add(b);
            return Task.CompletedTask;
        });
    });

    [RelayCommand]
    private Task CreateBackup() => RunAsync(async () =>
    {
        var info = await WithScopeAsync(sp => sp.GetRequiredService<BackupService>().CreateAsync());
        _dialog.Success("備份完成", $"已儲存：{info.FileName}");
        await RefreshAsync();
    });

    [RelayCommand]
    private Task Restore() => RunAsync(async () =>
    {
        if (Selected is null) { _dialog.Error("尚未選取", "請先選取要還原的備份"); return; }
        if (!_dialog.Confirm("還原備份", $"將還原至 {Selected.FileName}，此動作會覆蓋目前資料，確定要繼續嗎？")) return;
        await WithScopeAsync(sp => sp.GetRequiredService<BackupService>().RestoreAsync(Selected.AbsolutePath));
        _dialog.Success("還原完成", "備份已還原，建議重啟應用程式以重新載入資料");
    });

    [RelayCommand]
    private Task DeleteBackup() => RunAsync(async () =>
    {
        if (Selected is null) return;
        if (!_dialog.Confirm("刪除備份", $"確定刪除 {Selected.FileName}？")) return;
        await WithScopeAsync(sp =>
        {
            sp.GetRequiredService<BackupService>().Delete(Selected.AbsolutePath);
            return Task.CompletedTask;
        });
        await RefreshAsync();
    });
}
