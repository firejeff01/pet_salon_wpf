using System.IO.Compression;
using FluentAssertions;
using PetSalon.Core.Common;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public class BackupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _dbFile;
    private readonly string _backupDir;
    private readonly string _contractsDir;
    private readonly BackupService _svc;
    private readonly FakeClock _clock = new();

    public BackupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "backup-test-" + Guid.NewGuid().ToString("N"));
        _dbFile = Path.Combine(_root, "petsalon.db");
        _backupDir = Path.Combine(_root, "backups");
        _contractsDir = Path.Combine(_root, "contracts");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_contractsDir);

        File.WriteAllText(_dbFile, "DB-content-v1");

        var options = new BackupOptions
        {
            BackupDir = _backupDir,
            DbFilePath = _dbFile,
            ContractsDir = _contractsDir,
        };
        // BackupService 接 IPetSalonDbContext 但測試完全不碰 EF：給個 dummy。
        _svc = new BackupService(new DummyDbContext(), _clock, options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    // ===================== List =====================

    [Fact]
    public void List_returns_empty_when_no_backups()
    {
        _svc.List().Should().BeEmpty();
    }

    [Fact]
    public async Task List_returns_existing_backups_descending_by_time()
    {
        await _svc.CreateAsync();
        _clock.Now = _clock.Now.AddSeconds(2);
        await _svc.CreateAsync();

        var list = _svc.List();
        list.Should().HaveCount(2);
        list[0].CreatedAt.Should().BeAfter(list[1].CreatedAt);
    }

    // ===================== Create =====================

    [Fact]
    public async Task Create_produces_zip_file_with_timestamp_name()
    {
        var info = await _svc.CreateAsync();
        info.FileName.Should().StartWith("backup_").And.EndWith(".zip");
        File.Exists(info.AbsolutePath).Should().BeTrue();
    }

    [Fact]
    public async Task Create_zip_contains_db_file()
    {
        var info = await _svc.CreateAsync();
        using var zip = ZipFile.OpenRead(info.AbsolutePath);
        zip.Entries.Should().Contain(e => e.Name == "petsalon.db");
    }

    [Fact]
    public async Task Create_zip_includes_contract_pdfs_under_contracts_subfolder()
    {
        var subFolder = Path.Combine(_contractsDir, "20260601");
        Directory.CreateDirectory(subFolder);
        File.WriteAllBytes(Path.Combine(subFolder, "test.pdf"), new byte[] { 0x25, 0x50 });

        var info = await _svc.CreateAsync();
        using var zip = ZipFile.OpenRead(info.AbsolutePath);
        zip.Entries.Should().Contain(e => e.FullName == "contracts/20260601/test.pdf");
    }

    [Fact]
    public async Task Create_throws_when_db_file_missing()
    {
        File.Delete(_dbFile);
        await FluentActions.Awaiting(() => _svc.CreateAsync())
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "DB_NOT_FOUND");
    }

    // ===================== Restore =====================

    [Fact]
    public async Task Restore_overwrites_db_file()
    {
        File.WriteAllText(_dbFile, "Original");
        var info = await _svc.CreateAsync();
        File.WriteAllText(_dbFile, "Modified");

        await _svc.RestoreAsync(info.AbsolutePath);
        File.ReadAllText(_dbFile).Should().Be("Original");
    }

    [Fact]
    public async Task Restore_throws_when_backup_missing()
    {
        await FluentActions.Awaiting(() => _svc.RestoreAsync("C:\\nope.zip"))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "BACKUP_NOT_FOUND");
    }

    [Fact]
    public async Task Restore_extracts_contract_pdfs_back()
    {
        Directory.CreateDirectory(Path.Combine(_contractsDir, "20260601"));
        File.WriteAllBytes(Path.Combine(_contractsDir, "20260601", "x.pdf"), new byte[] { 1, 2, 3 });
        var info = await _svc.CreateAsync();

        // 砍掉 contracts
        Directory.Delete(_contractsDir, recursive: true);

        await _svc.RestoreAsync(info.AbsolutePath);
        File.Exists(Path.Combine(_contractsDir, "20260601", "x.pdf")).Should().BeTrue();
    }

    // ===================== Delete =====================

    [Fact]
    public async Task Delete_removes_backup_zip()
    {
        var info = await _svc.CreateAsync();
        _svc.Delete(info.AbsolutePath);
        File.Exists(info.AbsolutePath).Should().BeFalse();
    }

    [Fact]
    public void Delete_no_op_for_missing_file()
    {
        _svc.Delete(Path.Combine(_backupDir, "nope.zip"));
    }

    [Fact]
    public void Delete_rejects_path_outside_backup_dir()
    {
        var outside = Path.Combine(_root, "outside.zip");
        File.WriteAllBytes(outside, new byte[] { 1 });
        FluentActions.Invoking(() => _svc.Delete(outside))
            .Should().Throw<AppException>().Where(e => e.Code == "INVALID_PATH");
        File.Exists(outside).Should().BeTrue();   // 沒被刪
    }
}

internal sealed class DummyDbContext : PetSalon.Core.Abstractions.IPetSalonDbContext
{
    public Microsoft.EntityFrameworkCore.DbSet<PetSalon.Core.Entities.Owner> Owners => throw new NotImplementedException();
    public Microsoft.EntityFrameworkCore.DbSet<PetSalon.Core.Entities.Pet> Pets => throw new NotImplementedException();
    public Microsoft.EntityFrameworkCore.DbSet<PetSalon.Core.Entities.Appointment> Appointments => throw new NotImplementedException();
    public Microsoft.EntityFrameworkCore.DbSet<PetSalon.Core.Entities.GroomingRecord> GroomingRecords => throw new NotImplementedException();
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
}
