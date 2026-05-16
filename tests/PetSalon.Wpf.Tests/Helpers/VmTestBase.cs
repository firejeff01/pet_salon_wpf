using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Services;
using PetSalon.Infrastructure.Persistence;

namespace PetSalon.Wpf.Tests.Helpers;

/// <summary>
/// ViewModel 測試 fixture：建立一個真實的 DI ServiceProvider 接到 in-memory SQLite，
/// 然後從 IServiceScopeFactory 拿 scope，讓 ViewModel 行為跟正式 App 一致。
/// </summary>
public abstract class VmTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _root;
    public IServiceScopeFactory ScopeFactory { get; }
    public FakeClock Clock { get; } = new();
    public SequentialIdGenerator Ids { get; } = new();

    protected VmTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<PetSalonDbContext>(opt => opt.UseSqlite(_connection), ServiceLifetime.Scoped);
        services.AddScoped<IPetSalonDbContext>(sp => sp.GetRequiredService<PetSalonDbContext>());
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IIdGenerator>(Ids);
        services.AddSingleton<StoredValueService>();
        services.AddScoped<OwnerService>();
        services.AddScoped<PetService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<GroomingRecordService>();

        _root = services.BuildServiceProvider();
        ScopeFactory = _root.GetRequiredService<IServiceScopeFactory>();

        // 建立 schema
        using var scope = ScopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<PetSalonDbContext>().Database.EnsureCreated();
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    public void Dispose()
    {
        _root.Dispose();
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
