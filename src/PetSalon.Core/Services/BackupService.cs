using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;

namespace PetSalon.Core.Services;

public sealed class BackupService
{
    private readonly IPetSalonDbContext _db;
    private readonly IClock _clock;
    private readonly BackupOptions _options;

    public BackupService(IPetSalonDbContext db, IClock clock, BackupOptions options)
    {
        _db = db;
        _clock = clock;
        _options = options;
    }

    public IReadOnlyList<BackupFileInfo> List()
    {
        if (!Directory.Exists(_options.BackupDir)) return Array.Empty<BackupFileInfo>();
        return Directory.GetFiles(_options.BackupDir, "backup_*.zip")
            .Select(p =>
            {
                var info = new FileInfo(p);
                var ts = ParseTimestampFromFileName(info.Name) ?? info.LastWriteTime;
                return new BackupFileInfo(info.Name, info.FullName, ts, info.Length);
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    /// <summary>從 `backup_YYYYMMDD_HHmmss.zip` 解析時間戳，失敗則 fallback。</summary>
    private static DateTimeOffset? ParseTimestampFromFileName(string name)
    {
        const string prefix = "backup_";
        const string suffix = ".zip";
        if (!name.StartsWith(prefix) || !name.EndsWith(suffix)) return null;
        var body = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
        if (DateTime.TryParseExact(body, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.AssumeLocal, out var parsed))
            return new DateTimeOffset(parsed);
        return null;
    }

    public async Task<BackupFileInfo> CreateAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_options.BackupDir);
        var ts = _clock.Now.ToLocalTime().ToString("yyyyMMdd_HHmmss");
        var fileName = $"backup_{ts}.zip";
        var fullPath = Path.Combine(_options.BackupDir, fileName);

        if (!File.Exists(_options.DbFilePath))
            throw AppException.NotFound("DB_NOT_FOUND", $"找不到資料庫檔案 {_options.DbFilePath}");

        await using var fs = File.Create(fullPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);
        archive.CreateEntryFromFile(_options.DbFilePath, Path.GetFileName(_options.DbFilePath));

        if (Directory.Exists(_options.ContractsDir))
        {
            foreach (var pdf in Directory.GetFiles(_options.ContractsDir, "*.pdf", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(_options.ContractsDir, pdf).Replace('\\', '/');
                archive.CreateEntryFromFile(pdf, $"contracts/{rel}");
            }
        }

        var fi = new FileInfo(fullPath);
        return new BackupFileInfo(fi.Name, fi.FullName, fi.LastWriteTime, fi.Length);
        _ = ct;
    }

    public async Task RestoreAsync(string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath)) throw AppException.NotFound("BACKUP_NOT_FOUND", $"找不到備份 {zipPath}");

        // 關閉 DbContext 連線
        await _db.SaveChangesAsync(ct);

        var dbFile = _options.DbFilePath;
        var dbDir = Path.GetDirectoryName(dbFile)!;
        Directory.CreateDirectory(dbDir);

        await using var fs = File.OpenRead(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            if (entry.FullName == Path.GetFileName(dbFile))
            {
                entry.ExtractToFile(dbFile, overwrite: true);
            }
            else if (entry.FullName.StartsWith("contracts/", StringComparison.OrdinalIgnoreCase))
            {
                var rel = entry.FullName.Substring("contracts/".Length).Replace('/', Path.DirectorySeparatorChar);
                var dest = Path.Combine(_options.ContractsDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
            }
        }
    }

    public void Delete(string zipPath)
    {
        if (!File.Exists(zipPath)) return;
        var resolved = Path.GetFullPath(zipPath);
        var dir = Path.GetFullPath(_options.BackupDir);
        if (!resolved.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
            throw AppException.Unprocessable("INVALID_PATH", "備份檔案路徑不合法");
        File.Delete(resolved);
    }
}

public sealed class BackupOptions
{
    public string BackupDir { get; init; } = "backups";
    public string DbFilePath { get; init; } = "petsalon.db";
    public string ContractsDir { get; init; } = "contracts";
}
