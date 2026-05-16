using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

/// <summary>
/// BackupService 不直接讀 DbContext，所以這裡完全不接 EF：BackupOptions 指向假檔即可。
/// </summary>
public class BackupPageViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _dbFile;
    private readonly string _backupDir;
    private readonly string _contractsDir;
    private readonly ServiceProvider _provider;
    private readonly IServiceScopeFactory _scopes;
    private readonly FakeDialogService _dialog = new();
    private readonly FakeClock _clock = new();

    public BackupPageViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "backup-vm-" + Guid.NewGuid().ToString("N"));
        _dbFile = Path.Combine(_root, "petsalon.db");
        _backupDir = Path.Combine(_root, "backups");
        _contractsDir = Path.Combine(_root, "contracts");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_contractsDir);
        File.WriteAllText(_dbFile, "fake-db-content");

        var services = new ServiceCollection();
        services.AddSingleton<IPetSalonDbContext>(new VmTestBaseDummyDbContext());
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton(new BackupOptions
        {
            BackupDir = _backupDir,
            DbFilePath = _dbFile,
            ContractsDir = _contractsDir,
        });
        services.AddScoped<BackupService>();
        _provider = services.BuildServiceProvider();
        _scopes = _provider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private BackupPageViewModel CreateVm() => new(_scopes, _dialog);

    [Fact]
    public async Task Refresh_loads_empty_when_no_backups()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.Backups.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateBackup_creates_zip_and_shows_success()
    {
        var vm = CreateVm();
        await vm.CreateBackupCommand.ExecuteAsync(null);
        vm.Backups.Should().ContainSingle();
        _dialog.Successes.Should().ContainSingle().Which.title.Should().Be("備份完成");
    }

    [Fact]
    public async Task Restore_without_selection_shows_error()
    {
        var vm = CreateVm();
        await vm.RestoreCommand.ExecuteAsync(null);
        _dialog.Errors.Should().ContainSingle().Which.title.Should().Be("尚未選取");
    }

    [Fact]
    public async Task Restore_with_selection_confirms_and_shows_success()
    {
        var vm = CreateVm();
        await vm.CreateBackupCommand.ExecuteAsync(null);
        vm.Selected = vm.Backups[0];

        await vm.RestoreCommand.ExecuteAsync(null);

        _dialog.Confirms.Should().ContainSingle();
        _dialog.Successes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Restore_user_cancels_no_action()
    {
        var vm = CreateVm();
        await vm.CreateBackupCommand.ExecuteAsync(null);
        vm.Selected = vm.Backups[0];
        _dialog.Successes.Clear();
        _dialog.ConfirmResponse = (_, _) => false;

        await vm.RestoreCommand.ExecuteAsync(null);

        _dialog.Successes.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteBackup_removes_after_confirm()
    {
        var vm = CreateVm();
        await vm.CreateBackupCommand.ExecuteAsync(null);
        vm.Selected = vm.Backups[0];

        await vm.DeleteBackupCommand.ExecuteAsync(null);

        vm.Backups.Should().BeEmpty();
    }
}

internal sealed class VmTestBaseDummyDbContext : IPetSalonDbContext
{
    public Microsoft.EntityFrameworkCore.DbSet<PetSalon.Core.Entities.Owner> Owners => throw new NotImplementedException();
    public Microsoft.EntityFrameworkCore.DbSet<PetSalon.Core.Entities.Pet> Pets => throw new NotImplementedException();
    public Microsoft.EntityFrameworkCore.DbSet<PetSalon.Core.Entities.Appointment> Appointments => throw new NotImplementedException();
    public Microsoft.EntityFrameworkCore.DbSet<PetSalon.Core.Entities.GroomingRecord> GroomingRecords => throw new NotImplementedException();
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
}
