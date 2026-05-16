using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Services;
using PetSalon.Infrastructure.Identity;
using PetSalon.Infrastructure.Os;
using PetSalon.Infrastructure.Pdf;
using PetSalon.Infrastructure.Persistence;

namespace PetSalon.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPetSalonInfrastructure(
        this IServiceCollection services,
        string sqliteConnectionString,
        string contractOutputDir,
        string backupDir,
        string dbFilePath,
        string? chromiumCacheDir = null)
    {
        services.AddDbContext<PetSalonDbContext>(opt => opt.UseSqlite(sqliteConnectionString));
        services.AddScoped<IPetSalonDbContext>(sp => sp.GetRequiredService<PetSalonDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, NanoIdGenerator>();
        services.AddSingleton<IFileOpener, WindowsFileOpener>();

        // PDF: PuppeteerSharp 走原 pet_salon contract.hbs（1:1 對齊原版）
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Pdf", "Templates", "contract.hbs");
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Pdf", "Assets", "kaiu.ttf");
        var chromiumDir = chromiumCacheDir ?? Path.Combine(Path.GetTempPath(), "petsalon-chromium-cache");
        services.AddSingleton<IPdfGenerator>(_ => new PuppeteerSharpContractGenerator(templatePath, fontPath, chromiumDir));

        services.AddSingleton(new ContractOutputOptions { OutputDir = contractOutputDir });
        services.AddSingleton(new BackupOptions
        {
            BackupDir = backupDir,
            DbFilePath = dbFilePath,
            ContractsDir = contractOutputDir,
        });

        services.AddSingleton<StoredValueService>();
        services.AddScoped<OwnerService>();
        services.AddScoped<PetService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<GroomingRecordService>();
        services.AddScoped<ContractService>();
        services.AddScoped<BackupService>();

        return services;
    }
}
