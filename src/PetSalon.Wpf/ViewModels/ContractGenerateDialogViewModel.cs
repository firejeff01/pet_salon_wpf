using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class ContractGenerateDialogViewModel : ViewModelBase, IDialogResultProvider
{
    private readonly IDialogService _dialog;

    public ContractGenerateDialogViewModel(IServiceScopeFactory scope, IDialogService dialog) : base(scope)
    {
        _dialog = dialog;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(CanGenerate));
        };
    }

    public event Action<bool?>? RequestClose;

    [ObservableProperty] private string _groomingRecordId = string.Empty;
    [ObservableProperty] private string _caption = string.Empty;
    [ObservableProperty] private string? _generatedPath;
    [ObservableProperty] private ShopSignatureProfile? _selectedSignature;

    public ObservableCollection<ShopSignatureProfile> AvailableSignatures { get; } = new();
    private bool _loadingSignatures;
    private byte[]? _previewShopSignaturePng;

    /// <summary>對話框開啟時產出的預覽 PDF 路徑（temp dir）。Commit 時會刪除。</summary>
    [ObservableProperty] private string? _previewPath;

    /// <summary>WebView2 用的 file:// URI（綁定到 View 上的 WebView2.Source）。</summary>
    public Uri? PreviewUri => PreviewPath is null ? null : new Uri(PreviewPath);

    public bool CanGenerate => !IsBusy && PreviewUri is not null;

    partial void OnPreviewPathChanged(string? value)
    {
        OnPropertyChanged(nameof(PreviewUri));
        OnPropertyChanged(nameof(CanGenerate));
    }

    /// <summary>
    /// R2: 簽名板已從 UI 移除，此屬性設為 null 代表「不需簽名直接產生 PDF」。
    /// 若呼叫端明確設定此回呼（例如舊有測試），仍遵循舊有驗證邏輯（null/empty 回傳視為未簽名）。
    /// </summary>
    public Func<byte[]?>? CaptureOwnerSignature { get; set; }

    partial void OnSelectedSignatureChanged(ShopSignatureProfile? value)
    {
        if (!_loadingSignatures)
            _ = RunAsync(RegeneratePreviewAsync);
    }

    /// <summary>對話框開啟時載入預設簽名，產出預覽 PDF 並讓 View WebView2 載入。</summary>
    public Task LoadPreviewAsync() => RunAsync(async () =>
    {
        var profiles = await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>().ListAsync());
        _loadingSignatures = true;
        try
        {
            AvailableSignatures.Clear();
            foreach (var profile in profiles.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name))
                AvailableSignatures.Add(profile);
            SelectedSignature = AvailableSignatures.FirstOrDefault(x => x.IsDefault)
                ?? AvailableSignatures.FirstOrDefault();
        }
        finally
        {
            _loadingSignatures = false;
        }
        await RegeneratePreviewAsync();
    });

    [RelayCommand]
    private void ClearSignature() => SelectedSignature = null;

    private async Task RegeneratePreviewAsync()
    {
        var signaturePng = await ReadSelectedSignatureAsync(showWarning: true);
        var output = await WithScopeAsync(sp => sp.GetRequiredService<ContractService>()
            .PreviewAsync(GroomingRecordId, signaturePng));
        var oldPreview = PreviewPath;
        _previewShopSignaturePng = signaturePng;
        PreviewPath = output.AbsolutePath;
        DeletePreview(oldPreview);
    }

    private async Task<byte[]?> ReadSelectedSignatureAsync(bool showWarning)
    {
        if (SelectedSignature is null) return null;
        try
        {
            return await WithScopeAsync(sp => sp.GetRequiredService<ShopSignatureService>()
                .ReadPngAsync(SelectedSignature.SignatureId));
        }
        catch (PetSalon.Core.Common.AppException ex)
            when (ex.Code is "SIGNATURE_NOT_FOUND" or "SIGNATURE_UNAVAILABLE" or "INVALID_SIGNATURE_IMAGE")
        {
            if (showWarning)
                _dialog.Warning("簽名無法使用", $"{ex.Message}\n本次仍可產生 PDF，店家簽名處將留白。");
            return null;
        }
    }

    [RelayCommand]
    private Task Generate() => RunAsync(async () =>
    {
        // 若 CaptureOwnerSignature 有被設定（舊有路徑），使用舊有驗證邏輯。
        // 若未設定（R2 新路徑：簽名板已移除），直接以 null 產生 PDF。
        byte[]? sig = null;
        if (CaptureOwnerSignature is not null)
        {
            sig = CaptureOwnerSignature.Invoke();
            if (sig is null || sig.Length == 0)
                throw PetSalon.Core.Common.AppException.Unprocessable("MISSING_SIGNATURE", "請先完成飼主簽名");
        }

        // 正式輸出沿用預覽時已讀取的同一份 bytes，避免檔案在確認期間變動而導致結果不同。
        var shopSignaturePng = _previewShopSignaturePng;

        ContractGenerateResult result;
        if (sig is null && !string.IsNullOrEmpty(PreviewPath))
        {
            // 新流程：用 CommitPreviewAsync（重新產 PDF 到正式目錄並刪 preview）
            result = await WithScopeAsync(sp => sp.GetRequiredService<ContractService>()
                .CommitPreviewAsync(GroomingRecordId, PreviewPath, shopSignaturePng));
        }
        else
        {
            // 舊路徑：含簽名圖
            result = await WithScopeAsync(sp => sp.GetRequiredService<ContractService>().GenerateAsync(new ContractGenerateInput
            {
                GroomingRecordId = GroomingRecordId,
                OwnerSignaturePng = sig,
                ShopSignaturePng = shopSignaturePng,
            }));
        }
        GeneratedPath = result.AbsolutePath;
        _dialog.Success("已產生 PDF", $"契約 v{result.Version} 已輸出至：\n{result.AbsolutePath}");
        RequestClose?.Invoke(true);
    });

    [RelayCommand]
    private void Cancel()
    {
        DeletePreview(PreviewPath);
        RequestClose?.Invoke(false);
    }

    private static void DeletePreview(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
