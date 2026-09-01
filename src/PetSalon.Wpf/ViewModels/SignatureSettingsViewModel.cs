using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

/// <summary>簽名角色的 ComboBox 選項。</summary>
public sealed record SignatureRoleOption(SignatureRole Role, string Label)
{
    // ComboBoxItem 的 UI Automation Name 取自資料項的 ToString()，
    // 覆寫後輔助技術與 UI 測試看到的都是「美容人員／負責人」而非 record 預設字串。
    public override string ToString() => Label;
}

public partial class SignatureSettingsViewModel : ViewModelBase
{
    private readonly IDialogService _dialog;
    private readonly SignatureImageProcessor _imageProcessor;

    public SignatureSettingsViewModel(
        IServiceScopeFactory scope,
        IDialogService dialog,
        SignatureImageProcessor imageProcessor) : base(scope)
    {
        _dialog = dialog;
        _imageProcessor = imageProcessor;
    }

    public static IReadOnlyList<SignatureRoleOption> RoleOptions { get; } =
    [
        new(SignatureRole.Groomer, SignatureRoles.GroomerLabel),
        new(SignatureRole.Manager, SignatureRoles.ManagerLabel),
    ];

    public ObservableCollection<ShopSignatureProfile> Profiles { get; } = new();
    public Func<byte[]?>? CaptureHandwrittenSignature { get; set; }
    public Action? ClearHandwrittenSignature { get; set; }

    [ObservableProperty] private ShopSignatureProfile? _selectedProfile;
    [ObservableProperty] private ImageSource? _selectedPreview;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private SignatureRoleOption _newRole = RoleOptions[0];
    [ObservableProperty] private bool _makeNewDefault;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private SignatureRoleOption _editRole = RoleOptions[0];

    public override Task InitializeAsync() => RefreshAsync();

    partial void OnSelectedProfileChanged(ShopSignatureProfile? value)
    {
        EditName = value?.Name ?? string.Empty;
        EditRole = OptionFor(value?.Role ?? SignatureRole.Groomer);
        _ = LoadSelectedPreviewAsync(value);
    }

    private static SignatureRoleOption OptionFor(SignatureRole role)
        => RoleOptions.First(x => x.Role == role);

    private Task RefreshAsync(string? selectId = null) => RunAsync(async () =>
    {
        var list = await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().ListAsync());
        Profiles.Clear();
        // 先依角色分組（美容人員在前），組內預設優先。
        foreach (var profile in list
            .OrderBy(x => x.Role)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name))
        {
            Profiles.Add(profile);
        }
        SelectedProfile = Profiles.FirstOrDefault(x => x.SignatureId == selectId)
            ?? Profiles.FirstOrDefault(x => x.IsDefault)
            ?? Profiles.FirstOrDefault();
    });

    private Task LoadSelectedPreviewAsync(ShopSignatureProfile? profile) => RunAsync(async () =>
    {
        if (profile is null)
        {
            SelectedPreview = null;
            return;
        }
        var bytes = await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().ReadPngAsync(profile.SignatureId));
        SelectedPreview = SignatureImageProcessor.ToImageSource(bytes);
    });

    [RelayCommand]
    private Task SaveHandwritten() => RunAsync(async () =>
    {
        var captured = CaptureHandwrittenSignature?.Invoke();
        if (captured is null || captured.Length == 0)
            throw PetSalon.Core.Common.AppException.Validation("請先完成手寫簽名");
        var png = _imageProcessor.NormalizeToPng(captured);
        var role = NewRole.Role;
        var profile = await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>()
            .CreateAsync(NewName, role, png, MakeNewDefault));
        NewName = string.Empty;
        MakeNewDefault = false;
        ClearHandwrittenSignature?.Invoke();
        await RefreshAsync(profile.SignatureId);
        _dialog.Success("簽名已保存", $"{role.ToLabel()}簽名「{profile.Name}」已建立");
    });

    public Task ImportFileAsync(string path) => RunAsync(async () =>
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw PetSalon.Core.Common.AppException.NotFound("SIGNATURE_FILE_NOT_FOUND", "找不到選取的圖片");
        if (info.Length > 8L * 1024 * 1024)
            throw PetSalon.Core.Common.AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "簽名圖片超出大小限制");
        var input = await File.ReadAllBytesAsync(info.FullName);
        var png = _imageProcessor.NormalizeToPng(input);
        var role = NewRole.Role;
        var profile = await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>()
            .CreateAsync(NewName, role, png, MakeNewDefault));
        NewName = string.Empty;
        MakeNewDefault = false;
        await RefreshAsync(profile.SignatureId);
        _dialog.Success("簽名已匯入", $"{role.ToLabel()}簽名「{profile.Name}」已建立");
    });

    /// <summary>套用顯示名稱與角色的變更。</summary>
    [RelayCommand]
    private Task SaveEdits() => RunAsync(async () =>
    {
        if (SelectedProfile is null) return;
        var id = SelectedProfile.SignatureId;
        var name = EditName;
        var role = EditRole.Role;
        var roleChanged = SelectedProfile.Role != role;
        await WithScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<ShopSignatureService>();
            await svc.RenameAsync(id, name);
            if (roleChanged) await svc.ChangeRoleAsync(id, role);
        });
        await RefreshAsync(id);
    });

    [RelayCommand]
    private Task SetDefault() => RunAsync(async () =>
    {
        if (SelectedProfile is null) return;
        var id = SelectedProfile.SignatureId;
        var role = SelectedProfile.Role;
        await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().SetDefaultAsync(id));
        await RefreshAsync(id);
        _dialog.Success("已設為預設", $"此簽名已成為{role.ToLabel()}的預設簽名");
    });

    [RelayCommand]
    private Task Delete() => RunAsync(async () =>
    {
        if (SelectedProfile is null) return;
        var id = SelectedProfile.SignatureId;
        var name = SelectedProfile.Name;
        var role = SelectedProfile.Role;
        if (!_dialog.Confirm("刪除店家簽名", $"確定刪除{role.ToLabel()}簽名「{name}」嗎？此動作無法復原。")) return;
        await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().DeleteAsync(id));
        await RefreshAsync();
        _dialog.Success("已刪除", $"{role.ToLabel()}簽名「{name}」已刪除");
    });
}
