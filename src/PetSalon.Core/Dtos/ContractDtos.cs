namespace PetSalon.Core.Dtos;

public sealed class ContractGenerateInput
{
    public string GroomingRecordId { get; set; } = string.Empty;
    /// <summary>R2: 簽名板已移除，允許為 null 或空，產生 PDF 時以空白簽名處理。</summary>
    public byte[]? OwnerSignaturePng { get; set; } = null;
    /// <summary>本次契約要套用的店家簽名；無法讀取時可為 null，PDF 仍可產生。</summary>
    public byte[]? ShopSignaturePng { get; set; } = null;
}

public sealed record ContractGenerateResult(
    string FileName,
    string AbsolutePath,
    int Version,
    bool OpenedInViewer);
