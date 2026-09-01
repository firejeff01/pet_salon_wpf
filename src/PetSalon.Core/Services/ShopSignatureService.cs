using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;

namespace PetSalon.Core.Services;

public sealed class ShopSignatureService
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private readonly IShopSignatureStore _store;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ShopSignatureOptions _options;

    public ShopSignatureService(
        IShopSignatureStore store,
        IIdGenerator ids,
        IClock clock,
        ShopSignatureOptions options)
    {
        _store = store;
        _ids = ids;
        _clock = clock;
        _options = options;
    }

    public Task<IReadOnlyList<ShopSignatureProfile>> ListAsync(CancellationToken ct = default)
        => _store.ListAsync(ct);

    /// <summary>取得指定角色的所有簽名。</summary>
    public async Task<IReadOnlyList<ShopSignatureProfile>> ListAsync(SignatureRole role, CancellationToken ct = default)
        => (await _store.ListAsync(ct)).Where(x => x.Role == role).ToList();

    /// <summary>取得指定角色的預設簽名；該角色尚未建立任何簽名時回傳 null。</summary>
    public async Task<ShopSignatureProfile?> GetDefaultAsync(SignatureRole role, CancellationToken ct = default)
        => (await _store.ListAsync(ct)).FirstOrDefault(x => x.Role == role && x.IsDefault);

    public async Task<ShopSignatureProfile> CreateAsync(
        string name,
        SignatureRole role,
        byte[] pngBytes,
        bool makeDefault = false,
        CancellationToken ct = default)
    {
        ValidateName(name);
        ValidateRole(role);
        ValidatePng(pngBytes);
        var existing = await _store.ListAsync(ct);
        // 每個角色各自維護一組預設；該角色的第一組簽名自動成為預設。
        var shouldMakeDefault = makeDefault || existing.All(x => x.Role != role);
        var now = _clock.Now;
        var id = _ids.New("sig");
        var profile = new ShopSignatureProfile(
            id,
            name.Trim(),
            $"{id}.png",
            false,
            now,
            now)
        {
            Role = role,
        };
        await _store.CreateAsync(profile, pngBytes, ct);
        if (!shouldMakeDefault) return profile;
        await SetDefaultAsync(profile.SignatureId, ct);
        return profile with { IsDefault = true };
    }

    public async Task RenameAsync(string signatureId, string name, CancellationToken ct = default)
    {
        ValidateName(name);
        var profiles = (await _store.ListAsync(ct)).ToList();
        var index = IndexOf(profiles, signatureId);
        profiles[index] = profiles[index] with { Name = name.Trim(), UpdatedAt = _clock.Now };
        await _store.ReplaceProfilesAsync(profiles, ct);
    }

    /// <summary>
    /// 變更簽名角色。原角色若因此少了預設簽名會自動遞補，
    /// 新角色若原本沒有任何簽名則此筆自動成為該角色預設。
    /// </summary>
    public async Task ChangeRoleAsync(string signatureId, SignatureRole role, CancellationToken ct = default)
    {
        ValidateRole(role);
        var profiles = (await _store.ListAsync(ct)).ToList();
        var index = IndexOf(profiles, signatureId);
        if (profiles[index].Role == role) return;

        var now = _clock.Now;
        var becomesDefault = profiles.All(x => x.Role != role);
        profiles[index] = profiles[index] with { Role = role, IsDefault = becomesDefault, UpdatedAt = now };
        await _store.ReplaceProfilesAsync(NormalizeDefaults(profiles, now), ct);
    }

    public async Task SetDefaultAsync(string signatureId, CancellationToken ct = default)
    {
        var profiles = (await _store.ListAsync(ct)).ToList();
        var role = profiles.FirstOrDefault(x => x.SignatureId == signatureId)?.Role
            ?? throw AppException.NotFound("SIGNATURE_NOT_FOUND", "找不到店家簽名");
        var now = _clock.Now;
        // 只影響同角色的簽名，另一角色的預設維持不變。
        var updated = profiles
            .Select(x => x.Role != role ? x : x with { IsDefault = x.SignatureId == signatureId, UpdatedAt = now })
            .ToList();
        await _store.ReplaceProfilesAsync(updated, ct);
    }

    public async Task DeleteAsync(string signatureId, CancellationToken ct = default)
    {
        var profiles = (await _store.ListAsync(ct)).ToList();
        var profile = profiles.FirstOrDefault(x => x.SignatureId == signatureId)
            ?? throw AppException.NotFound("SIGNATURE_NOT_FOUND", "找不到店家簽名");
        var remaining = NormalizeDefaults(
            profiles.Where(x => x.SignatureId != signatureId).ToList(),
            _clock.Now);
        await _store.DeleteAsync(profile, remaining, ct);
    }

    public async Task<byte[]> ReadPngAsync(string signatureId, CancellationToken ct = default)
    {
        var profile = (await _store.ListAsync(ct)).FirstOrDefault(x => x.SignatureId == signatureId)
            ?? throw AppException.NotFound("SIGNATURE_NOT_FOUND", "找不到店家簽名");
        try
        {
            var bytes = await _store.ReadPngAsync(profile, ct);
            ValidatePng(bytes);
            return bytes;
        }
        catch (AppException) { throw; }
        catch (Exception)
        {
            throw AppException.Unprocessable("SIGNATURE_UNAVAILABLE", "店家簽名檔遺失、損毀或無法讀取");
        }
    }

    /// <summary>確保每個角色恰有一組預設簽名（該角色仍有簽名時）。</summary>
    private static List<ShopSignatureProfile> NormalizeDefaults(
        List<ShopSignatureProfile> profiles,
        DateTimeOffset now)
    {
        var result = profiles;
        foreach (var group in profiles.GroupBy(x => x.Role).ToList())
        {
            if (group.Count(x => x.IsDefault) == 1) continue;
            var defaultId = group.FirstOrDefault(x => x.IsDefault)?.SignatureId ?? group.First().SignatureId;
            result = result
                .Select(x => x.Role != group.Key
                    ? x
                    : x with { IsDefault = x.SignatureId == defaultId, UpdatedAt = now })
                .ToList();
        }
        return result;
    }

    private static int IndexOf(List<ShopSignatureProfile> profiles, string signatureId)
    {
        var index = profiles.FindIndex(x => x.SignatureId == signatureId);
        if (index < 0) throw AppException.NotFound("SIGNATURE_NOT_FOUND", "找不到店家簽名");
        return index;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw AppException.Validation("簽名名稱為必填");
        if (name.Trim().Length > 80) throw AppException.Validation("簽名名稱不可超過 80 字");
    }

    private static void ValidateRole(SignatureRole role)
    {
        if (!Enum.IsDefined(role)) throw AppException.Validation("簽名角色不正確");
    }

    private void ValidatePng(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < PngHeader.Length ||
            !bytes.AsSpan(0, PngHeader.Length).SequenceEqual(PngHeader))
            throw AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "簽名圖片格式不正確");
        if (bytes.Length > _options.MaxFileBytes)
            throw AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "簽名圖片超出大小限制");
    }
}

public sealed class ShopSignatureOptions
{
    public string RootDir { get; init; } = "signatures";
    public int MaxFileBytes { get; init; } = 2 * 1024 * 1024;
    public int MaxPixelWidth { get; init; } = 2048;
    public int MaxPixelHeight { get; init; } = 1024;
}
