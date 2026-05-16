using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Infrastructure.Persistence;

namespace PetSalon.Core.Tests.Helpers;

/// <summary>
/// 共享 in-memory SQLite 測試 fixture。每個測試類別 new 一份新的，所以 DB 完全隔離。
/// 連線保持 open 直到 Dispose；關閉後資料庫銷毀。
/// </summary>
public abstract class ServiceTestBase : IDisposable
{
    protected readonly PetSalonDbContext Db;
    protected readonly FakeClock Clock = new();
    protected readonly SequentialIdGenerator Ids = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    protected ServiceTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PetSalonDbContext>()
            .UseSqlite(_connection)
            .Options;
        Db = new PetSalonDbContext(options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Db.Dispose();
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.FromHours(8));
}

public sealed class SequentialIdGenerator : IIdGenerator
{
    private int _counter;
    public string New(string prefix) => $"{prefix}_{++_counter:D4}";
}
