using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _nav;
    private readonly UpdateChecker _updater;
    private readonly UpdateInstaller _updateInstaller;
    private readonly IDialogService _dialog;
    private UpdateInfo? _pendingUpdate;

    public MainViewModel(
        INavigationService nav,
        UpdateChecker updater,
        UpdateInstaller updateInstaller,
        IDialogService dialog)
    {
        _nav = nav;
        _updater = updater;
        _updateInstaller = updateInstaller;
        _dialog = dialog;
        AppVersion = UpdateChecker.CurrentVersion().ToString(3);
        WindowTitle = $"貳寶寵物美容工坊 v{AppVersion} — 犬貓美容定型化契約系統";
        _nav.CurrentViewModelChanged += vm =>
        {
            CurrentView = vm;
            CurrentTitle = ResolveTitle(vm);
            UpdateActiveMenu(vm);
        };
        // 對齊原 pet_salon App.vue：5 個頂層導覽
        MenuItems = new ObservableCollection<MenuEntry>
        {
            new("home", "首頁"),
            new("owners", "飼主管理"),
            new("calendar", "預約日曆"),
            new("customer", "客戶填寫"),
            new("backup", "備份管理"),
            new("signatures", "簽名設定"),
        };
        _ = CheckForUpdateAsync();
    }

    public string AppVersion { get; }
    public string WindowTitle { get; }

    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private string _updateBannerText = string.Empty;
    [ObservableProperty] private string _updateDownloadUrl = string.Empty;
    [ObservableProperty] private bool _isUpdating;

    /// <summary>可自動安裝時按鈕寫「立即更新」，否則退回舊行為的「下載新版」。</summary>
    public string UpdateActionText => _pendingUpdate?.CanAutoInstall == true ? "立即更新" : "下載新版";

    private async Task CheckForUpdateAsync()
    {
        var info = await _updater.CheckAsync();
        if (info is null) return;
        // 一定要回 UI thread 寫 ObservableProperty
        await App.Current.Dispatcher.InvokeAsync(() =>
        {
            _pendingUpdate = info;
            UpdateBannerText = info.CanAutoInstall
                ? $"🎉 有新版 v{info.Latest} 可用（目前 v{AppVersion}），點「立即更新」自動安裝"
                : $"🎉 有新版 v{info.Latest} 可用（目前 v{AppVersion}），點此下載";
            UpdateDownloadUrl = info.ReleasePageUrl;
            OnPropertyChanged(nameof(UpdateActionText));
            IsUpdateAvailable = true;
        });
    }

    [RelayCommand]
    private async Task OpenUpdate()
    {
        var info = _pendingUpdate;
        // 沒有 MSI 或校驗檔就不冒險自動執行，直接開 release 頁讓使用者手動處理
        if (info is null || !info.CanAutoInstall)
        {
            OpenReleasePage();
            return;
        }

        if (IsUpdating) return;
        IsUpdating = true;
        var original = UpdateBannerText;
        try
        {
            var progress = new Progress<double>(p =>
                UpdateBannerText = $"⬇ 正在下載 v{info.Latest}⋯⋯ {p:P0}");
            UpdateBannerText = $"⬇ 正在下載 v{info.Latest}⋯⋯";

            var msiPath = await _updateInstaller.DownloadVerifiedAsync(info, progress);

            UpdateBannerText = "✅ 下載完成，即將關閉程式並開始安裝⋯⋯";
            _updateInstaller.Launch(msiPath);
            // 必須讓出檔案佔用，否則 MSI 升級會卡在「使用中的檔案」
            App.Current.Shutdown();
        }
        catch (Exception ex)
        {
            IsUpdating = false;
            UpdateBannerText = original;
            _dialog.Warning(
                "自動更新未完成",
                $"{ex.Message}\n\n將為你開啟下載頁面，可改用手動安裝。");
            OpenReleasePage();
        }
    }

    private void OpenReleasePage()
    {
        if (string.IsNullOrWhiteSpace(UpdateDownloadUrl)) return;
        try { Process.Start(new ProcessStartInfo(UpdateDownloadUrl) { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void DismissUpdate() => IsUpdateAvailable = false;

    public ObservableCollection<MenuEntry> MenuItems { get; }

    [ObservableProperty] private ViewModelBase? _currentView;
    [ObservableProperty] private string _currentTitle = string.Empty;

    [RelayCommand]
    private void Navigate(string? key)
    {
        switch (key)
        {
            case "home": _nav.NavigateTo<HomeViewModel>(); break;
            case "owners": _nav.NavigateTo<OwnerPageViewModel>(); break;
            case "calendar": _nav.NavigateTo<CalendarViewModel>(); break;
            case "customer": _nav.NavigateTo<CustomerFormViewModel>(); break;
            case "backup": _nav.NavigateTo<BackupPageViewModel>(); break;
            case "signatures": _nav.NavigateTo<SignatureSettingsViewModel>(); break;
        }
    }

    [RelayCommand]
    private void Reload()
    {
        _ = CurrentView?.InitializeAsync();
    }

    public void GoHome() => _nav.NavigateTo<HomeViewModel>();

    private void UpdateActiveMenu(ViewModelBase vm)
    {
        var key = vm switch
        {
            HomeViewModel => "home",
            OwnerPageViewModel or PetEditViewModel => "owners",
            CalendarViewModel or DailyAppointmentsViewModel or AppointmentEditViewModel or GroomingPageViewModel => "calendar",
            CustomerFormViewModel => "customer",
            BackupPageViewModel => "backup",
            SignatureSettingsViewModel => "signatures",
            _ => null,
        };
        foreach (var m in MenuItems) m.IsActive = m.Key == key;
    }

    private static string ResolveTitle(ViewModelBase vm) => vm switch
    {
        HomeViewModel => "首頁",
        OwnerPageViewModel => "飼主管理",
        PetEditViewModel => "寵物資料",
        CalendarViewModel => "預約日曆",
        DailyAppointmentsViewModel => "本日預約",
        AppointmentEditViewModel => "預約資料",
        GroomingPageViewModel => "美容紀錄",
        CustomerFormViewModel => "客戶填寫",
        BackupPageViewModel => "備份管理",
        SignatureSettingsViewModel => "店家簽名設定",
        _ => string.Empty,
    };
}

public partial class MenuEntry : ObservableObject
{
    public MenuEntry(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }
    [ObservableProperty] private bool _isActive;
}
