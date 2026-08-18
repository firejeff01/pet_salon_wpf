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

    public async Task<ShopSignatureProfile> CreateAsync(
        string name,
        byte[] pngBytes,
        bool makeDefault = false,
        CancellationToken ct = default)
    {
        ValidateName(name);
        ValidatePng(pngBytes);
        var existing = await _store.ListAsync(ct);
        var shouldMakeDefault = makeDefault || existing.Count == 0;
        var now = _clock.Now;
        var id = _ids.New("sig");
        var profile = new ShopSignatureProfile(
            id,
            name.Trim(),
            $"{id}.png",
            false,
            now,
            now);
        await _store.CreateAsync(profile, pngBytes, ct);
        if (!shouldMakeDefault) return profile;
        await SetDefaultAsync(profile.SignatureId, ct);
        return profile with { IsDefault = true };
    }

    public async Task RenameAsync(string signatureId, string name, CancellationToken ct = default)
    {
        ValidateName(name);
        var profiles = (await _store.ListAsync(ct)).ToList();
        var index = profiles.FindIndex(x => x.SignatureId == signatureId);
        if (index < 0) throw AppException.NotFound("SIGNATURE_NOT_FOUND", "找不到店家簽名");
        profiles[index] = profiles[index] with { Name = name.Trim(), UpdatedAt = _clock.Now };
        await _store.ReplaceProfilesAsync(profiles, ct);
    }

    public async Task SetDefaultAsync(string signatureId, CancellationToken ct = default)
    {
        var profiles = (await _store.ListAsync(ct)).ToList();
        if (profiles.All(x => x.SignatureId != signatureId))
            throw AppException.NotFound("SIGNATURE_NOT_FOUND", "找不到店家簽名");
        var now = _clock.Now;
        var updated = profiles
            .Select(x => x with { IsDefault = x.SignatureId == signatureId, UpdatedAt = now })
            .ToList();
        await _store.ReplaceProfilesAsync(updated, ct);
    }

    public async Task DeleteAsync(string signatureId, CancellationToken ct = default)
    {
        var profiles = (await _store.ListAsync(ct)).ToList();
        var profile = profiles.FirstOrDefault(x => x.SignatureId == signatureId)
            ?? throw AppException.NotFound("SIGNATURE_NOT_FOUND", "找不到店家簽名");
        var remaining = profiles.Where(x => x.SignatureId != signatureId).ToList();
        if (remaining.Count > 0 && remaining.Count(x => x.IsDefault) != 1)
        {
            var defaultId = remaining.FirstOrDefault(x => x.IsDefault)?.SignatureId
                ?? remaining[0].SignatureId;
            remaining = remaining
                .Select(x => x with
                {
                    IsDefault = x.SignatureId == defaultId,
                    UpdatedAt = _clock.Now,
                })
                .ToList();
        }
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

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw AppException.Validation("簽名名稱為必填");
        if (name.Trim().Length > 80) throw AppException.Validation("簽名名稱不可超過 80 字");
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
