using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PetSalon.Infrastructure;
using PetSalon.Infrastructure.Persistence;
using PetSalon.Wpf.Services;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf;

public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not started");

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI test 可透過環境變數覆寫資料夾，避免污染使用者真實資料
        var appDataOverride = Environment.GetEnvironmentVariable("PETSALON_APP_DATA");
        var appData = !string.IsNullOrEmpty(appDataOverride)
            ? appDataOverride
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PetSalon");
        Directory.CreateDirectory(appData);
        // 使用獨立檔名，避免跟 pet_salon2_wpf 舊版 schema 衝突
        var dbPath = Path.Combine(appData, "petsalon_app.db");
        var contractsDir = Path.Combine(appData, "contracts");
        var backupDir = Path.Combine(appData, "backups");
        var chromiumDir = Path.Combine(appData, "chromium");
        Directory.CreateDirectory(contractsDir);
        Directory.CreateDirectory(backupDir);
        Directory.CreateDirectory(chromiumDir);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddPetSalonInfrastructure($"Data Source={dbPath}", contractsDir, backupDir, dbPath, chromiumDir);
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<UpdateChecker>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<HomeViewModel>();
                services.AddTransient<OwnerPageViewModel>();
                services.AddTransient<PetEditViewModel>();
                services.AddTransient<CalendarViewModel>();
                services.AddTransient<DailyAppointmentsViewModel>();
                services.AddTransient<AppointmentEditViewModel>();
                services.AddTransient<GroomingPageViewModel>();
                services.AddTransient<CustomerFormViewModel>();
                services.AddTransient<BackupPageViewModel>();
                services.AddTransient<ContractGenerateDialogViewModel>();
            })
            .Build();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetSalonDbContext>();
            db.Database.EnsureCreated();
            BackupBeforeSchemaPatchIfNeeded(db, dbPath, backupDir);
            EnsurePhysicalExaminationNoteColumns(db);
        }

        var main = new MainWindow
        {
            DataContext = _host.Services.GetRequiredService<MainViewModel>(),
        };
        main.Show();

        ((MainViewModel)main.DataContext).GoHome();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// 在跑任何 idempotent ALTER 之前先做安全備份：偵測 DB 缺少預期欄位（代表這次啟動會動 schema），
    /// 就把 DB 複製到 backups\auto_v{appVersion}_{yyyyMMddHHmmss}.db。保留最近 5 個自動備份。
    /// 若 DB 已是最新 schema，不會重複備份。失敗不阻擋啟動。
    /// </summary>
    private static void BackupBeforeSchemaPatchIfNeeded(PetSalonDbContext db, string dbPath, string backupDir)
    {
        try
        {
            if (!File.Exists(dbPath)) return;

            // 用「是否含 phys_eyes_note」當作 schema marker：缺 = 升級中、需備份
            var hasMarker = false;
            try
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('pets') WHERE name='phys_eyes_note'";
                hasMarker = Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
            }
            catch { /* 視為 schema 缺 = 走備份 */ }
            if (hasMarker) return;

            Directory.CreateDirectory(backupDir);
            var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var dest = Path.Combine(backupDir, $"auto_v{ver}_{stamp}.db");
            File.Copy(dbPath, dest, overwrite: false);

            // 只留最近 5 個 auto_*.db
            var older = Directory.GetFiles(backupDir, "auto_v*.db")
                .OrderByDescending(File.GetCreationTimeUtc)
                .Skip(5);
            foreach (var f in older) { try { File.Delete(f); } catch { } }
        }
        catch { /* 備份失敗不阻擋 app 啟動 */ }
    }

    // 既有 DB 從 EnsureCreated 建立、無 __EFMigrationsHistory，無法走 Migrate()；
    // 此處針對 Phase 1 / R6 新增之 PhysicalExamination *Note 欄位做 idempotent ALTER。
    private static void EnsurePhysicalExaminationNoteColumns(PetSalonDbContext db)
    {
        string[] tables = { "pets", "grooming_records" };
        string[] cols = { "phys_eyes_note", "phys_ears_note", "phys_teeth_note", "phys_limbs_note", "phys_skin_note", "phys_fur_note" };
        foreach (var t in tables)
        foreach (var c in cols)
        {
            var sql = "ALTER TABLE " + t + " ADD COLUMN " + c + " TEXT NULL";
            try { db.Database.ExecuteSqlRaw(sql); }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1) { /* duplicate column — ignore */ }
        }
    }
}
