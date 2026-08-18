using System.Text.Json;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;

namespace PetSalon.Infrastructure.Signatures;

public sealed class FileShopSignatureStore : IShopSignatureStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _rootDir;
    private readonly string _profilesPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileShopSignatureStore(ShopSignatureOptions options)
    {
        _rootDir = Path.GetFullPath(options.RootDir);
        _profilesPath = Path.Combine(_rootDir, "profiles.json");
    }

    public async Task<IReadOnlyList<ShopSignatureProfile>> ListAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return await ReadProfilesCoreAsync(ct); }
        finally { _gate.Release(); }
    }

    public async Task CreateAsync(ShopSignatureProfile profile, byte[] pngBytes, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_rootDir);
            var profiles = (await ReadProfilesCoreAsync(ct)).ToList();
            if (profiles.Any(x => x.SignatureId == profile.SignatureId))
                throw AppException.Conflict("SIGNATURE_EXISTS", "店家簽名識別碼重複");

            var imagePath = ResolveChild(profile.ImageFileName);
            var imageTemp = ResolveChild($".{profile.SignatureId}.{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllBytesAsync(imageTemp, pngBytes, ct);
                File.Move(imageTemp, imagePath, overwrite: false);
                profiles.Add(profile);
                await WriteProfilesCoreAsync(profiles, ct);
            }
            catch
            {
                TryDelete(imageTemp);
                TryDelete(imagePath);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task ReplaceProfilesAsync(IReadOnlyList<ShopSignatureProfile> profiles, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { await WriteProfilesCoreAsync(profiles, ct); }
        finally { _gate.Release(); }
    }

    public async Task<byte[]> ReadPngAsync(ShopSignatureProfile profile, CancellationToken ct = default)
    {
        var path = ResolveChild(profile.ImageFileName);
        if (!File.Exists(path)) throw AppException.Unprocessable("SIGNATURE_UNAVAILABLE", "店家簽名檔不存在");
        try { return await File.ReadAllBytesAsync(path, ct); }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            throw AppException.Unprocessable("SIGNATURE_UNAVAILABLE", "店家簽名檔無法讀取");
        }
    }

    public async Task DeleteAsync(
        ShopSignatureProfile profile,
        IReadOnlyList<ShopSignatureProfile> remainingProfiles,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await WriteProfilesCoreAsync(remainingProfiles, ct);
            TryDelete(ResolveChild(profile.ImageFileName));
        }
        finally { _gate.Release(); }
    }

    private async Task<List<ShopSignatureProfile>> ReadProfilesCoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_profilesPath)) return new List<ShopSignatureProfile>();
        try
        {
            await using var stream = File.OpenRead(_profilesPath);
            return await JsonSerializer.DeserializeAsync<List<ShopSignatureProfile>>(stream, JsonOptions, ct)
                ?? new List<ShopSignatureProfile>();
        }
        catch (JsonException)
        {
            throw AppException.Unprocessable("SIGNATURE_METADATA_INVALID", "店家簽名設定檔已損毀");
        }
    }

    private async Task WriteProfilesCoreAsync(IReadOnlyList<ShopSignatureProfile> profiles, CancellationToken ct)
    {
        Directory.CreateDirectory(_rootDir);
        foreach (var profile in profiles) _ = ResolveChild(profile.ImageFileName);
        var temp = ResolveChild($".profiles.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, profiles, JsonOptions, ct);
            File.Move(temp, _profilesPath, overwrite: true);
        }
        finally { TryDelete(temp); }
    }

    private string ResolveChild(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            throw AppException.Unprocessable("INVALID_PATH", "店家簽名檔案路徑不合法");
        var resolved = Path.GetFullPath(Path.Combine(_rootDir, fileName));
        var prefix = _rootDir.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDir
            : _rootDir + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw AppException.Unprocessable("INVALID_PATH", "店家簽名檔案路徑不合法");
        return resolved;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
