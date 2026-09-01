namespace PetSalon.Core.Dtos;

/// <summary>店家簽名的角色。契約 PDF 依角色帶入不同欄位。</summary>
public enum SignatureRole
{
    /// <summary>美容人員（契約第一頁「美容人員簽名」欄位）。</summary>
    Groomer = 0,

    /// <summary>負責人（契約最後一頁「乙方簽章」欄位）。</summary>
    Manager = 1,
}

public sealed record ShopSignatureProfile(
    string SignatureId,
    string Name,
    string ImageFileName,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>
    /// 簽名角色。以 init 屬性（而非 positional 參數）宣告，
    /// 讓升級前寫入、沒有 role 欄位的 profiles.json 反序列化後自動落在 <see cref="SignatureRole.Groomer"/>。
    /// </summary>
    public SignatureRole Role { get; init; } = SignatureRole.Groomer;
}

public static class SignatureRoles
{
    public const string GroomerLabel = "美容人員";
    public const string ManagerLabel = "負責人";

    public static string ToLabel(this SignatureRole role) => role switch
    {
        SignatureRole.Manager => ManagerLabel,
        _ => GroomerLabel,
    };
}
