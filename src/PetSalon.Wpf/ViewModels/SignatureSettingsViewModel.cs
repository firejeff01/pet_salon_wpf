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

    public ObservableCollection<ShopSignatureProfile> Profiles { get; } = new();
    public Func<byte[]?>? CaptureHandwrittenSignature { get; set; }
    public Action? ClearHandwrittenSignature { get; set; }

    [ObservableProperty] private ShopSignatureProfile? _selectedProfile;
    [ObservableProperty] private ImageSource? _selectedPreview;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private bool _makeNewDefault;
    [ObservableProperty] private string _editName = string.Empty;

    public override Task InitializeAsync() => RefreshAsync();

    partial void OnSelectedProfileChanged(ShopSignatureProfile? value)
    {
        EditName = value?.Name ?? string.Empty;
        _ = LoadSelectedPreviewAsync(value);
    }

    private Task RefreshAsync(string? selectId = null) => RunAsync(async () =>
    {
        var list = await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().ListAsync());
        Profiles.Clear();
        foreach (var profile in list.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name))
            Profiles.Add(profile);
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
        var profile = await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>()
            .CreateAsync(NewName, png, MakeNewDefault));
        NewName = string.Empty;
        MakeNewDefault = false;
        ClearHandwrittenSignature?.Invoke();
        await RefreshAsync(profile.SignatureId);
        _dialog.Success("簽名已保存", $"店家簽名「{profile.Name}」已建立");
    });

    public Task ImportFileAsync(string path) => RunAsync(async () =>
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw PetSalon.Core.Common.AppException.NotFound("SIGNATURE_FILE_NOT_FOUND", "找不到選取的圖片");
        if (info.Length > 8L * 1024 * 1024)
            throw PetSalon.Core.Common.AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "簽名圖片超出大小限制");
        var input = await File.ReadAllBytesAsync(info.FullName);
        var png = _imageProcessor.NormalizeToPng(input);
        var profile = await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>()
            .CreateAsync(NewName, png, MakeNewDefault));
        NewName = string.Empty;
        MakeNewDefault = false;
        await RefreshAsync(profile.SignatureId);
        _dialog.Success("簽名已匯入", $"店家簽名「{profile.Name}」已建立");
    });

    [RelayCommand]
    private Task Rename() => RunAsync(async () =>
    {
        if (SelectedProfile is null) return;
        var id = SelectedProfile.SignatureId;
        await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().RenameAsync(id, EditName));
        await RefreshAsync(id);
    });

    [RelayCommand]
    private Task SetDefault() => RunAsync(async () =>
    {
        if (SelectedProfile is null) return;
        var id = SelectedProfile.SignatureId;
        await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().SetDefaultAsync(id));
        await RefreshAsync(id);
    });

    [RelayCommand]
    private Task Delete() => RunAsync(async () =>
    {
        if (SelectedProfile is null) return;
        var id = SelectedProfile.SignatureId;
        var name = SelectedProfile.Name;
        if (!_dialog.Confirm("刪除店家簽名", $"確定刪除「{name}」嗎？此動作無法復原。")) return;
        await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().DeleteAsync(id));
        await RefreshAsync();
        _dialog.Success("已刪除", $"店家簽名「{name}」已刪除");
    });
}
