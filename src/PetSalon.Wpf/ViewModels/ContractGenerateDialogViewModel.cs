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
    }

    public event Action<bool?>? RequestClose;

    [ObservableProperty] private string _groomingRecordId = string.Empty;
    [ObservableProperty] private string _caption = string.Empty;
    [ObservableProperty] private string? _generatedPath;

    /// <summary>對話框開啟時產出的預覽 PDF 路徑（temp dir）。Commit 時會刪除。</summary>
    [ObservableProperty] private string? _previewPath;

    /// <summary>WebView2 用的 file:// URI（綁定到 View 上的 WebView2.Source）。</summary>
    public Uri? PreviewUri => PreviewPath is null ? null : new Uri(PreviewPath);

    partial void OnPreviewPathChanged(string? value) => OnPropertyChanged(nameof(PreviewUri));

    /// <summary>
    /// R2: 簽名板已從 UI 移除，此屬性設為 null 代表「不需簽名直接產生 PDF」。
    /// 若呼叫端明確設定此回呼（例如舊有測試），仍遵循舊有驗證邏輯（null/empty 回傳視為未簽名）。
    /// </summary>
    public Func<byte[]?>? CaptureOwnerSignature { get; set; }

    /// <summary>對話框開啟時呼叫，產出預覽 PDF 並讓 View WebView2 載入。</summary>
    public Task LoadPreviewAsync() => RunAsync(async () =>
    {
        var output = await WithScopeAsync(sp => sp.GetRequiredService<ContractService>().PreviewAsync(GroomingRecordId));
        PreviewPath = output.AbsolutePath;
    });

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

        ContractGenerateResult result;
        if (sig is null && !string.IsNullOrEmpty(PreviewPath))
        {
            // 新流程：用 CommitPreviewAsync（重新產 PDF 到正式目錄並刪 preview）
            result = await WithScopeAsync(sp => sp.GetRequiredService<ContractService>().CommitPreviewAsync(GroomingRecordId, PreviewPath));
        }
        else
        {
            // 舊路徑：含簽名圖
            result = await WithScopeAsync(sp => sp.GetRequiredService<ContractService>().GenerateAsync(new ContractGenerateInput
            {
                GroomingRecordId = GroomingRecordId,
                OwnerSignaturePng = sig,
            }));
        }
        GeneratedPath = result.AbsolutePath;
        _dialog.Success("已產生 PDF", $"契約 v{result.Version} 已輸出至：\n{result.AbsolutePath}");
        RequestClose?.Invoke(true);
    });

    [RelayCommand]
    private void Cancel()
    {
        if (!string.IsNullOrEmpty(PreviewPath))
        {
            try { if (System.IO.File.Exists(PreviewPath)) System.IO.File.Delete(PreviewPath); } catch { }
        }
        RequestClose?.Invoke(false);
    }
}
