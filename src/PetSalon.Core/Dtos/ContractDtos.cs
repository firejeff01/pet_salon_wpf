namespace PetSalon.Core.Dtos;

/// <summary>
/// 本次契約要套用的店家簽名。美容人員與負責人各自獨立，
/// 任一方為 null 時該欄位在 PDF 留白，仍可產生 PDF。
/// </summary>
public sealed record ContractShopSignatures(byte[]? GroomerPng = null, byte[]? ManagerPng = null)
{
    public static readonly ContractShopSignatures None = new();
}

public sealed class ContractGenerateInput
{
    public string GroomingRecordId { get; set; } = string.Empty;
    /// <summary>R2: 簽名板已移除，允許為 null 或空，產生 PDF 時以空白簽名處理。</summary>
    public byte[]? OwnerSignaturePng { get; set; } = null;
    /// <summary>本次契約要套用的美容人員簽名；無法讀取時可為 null，PDF 仍可產生。</summary>
    public byte[]? GroomerSignaturePng { get; set; } = null;
    /// <summary>本次契約要套用的負責人簽名；無法讀取時可為 null，PDF 仍可產生。</summary>
    public byte[]? ManagerSignaturePng { get; set; } = null;
}

public sealed record ContractGenerateResult(
    string FileName,
    string AbsolutePath,
    int Version,
    bool OpenedInViewer);
