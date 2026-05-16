using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace PetSalon.Wpf.UiTests.Helpers;

/// <summary>
/// 為單一測試類別啟動 WPF 應用程式，並在獨立的暫存資料夾下作業（每次測試類別新建資料夾）。
/// Dispose 時關閉應用程式並清掉暫存資料夾。
/// </summary>
public sealed class AppFixture : IDisposable
{
    public Application App { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }
    public string AppDataDir { get; }

    public AppFixture()
    {
        AppDataDir = Path.Combine(Path.GetTempPath(), "petsalon-uitest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(AppDataDir);

        var exePath = FindExe();
        var psi = new ProcessStartInfo(exePath)
        {
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = false,
        };
        psi.EnvironmentVariables["PETSALON_APP_DATA"] = AppDataDir;

        App = Application.Launch(psi);
        Automation = new UIA3Automation();
        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(20))
            ?? throw new InvalidOperationException("Main window not found within 20s");

        // 等首頁初始載入完成
        WaitForFrameRendered();
    }

    private static string FindExe()
    {
        // 從測試 bin 目錄回推到 PetSalon.Wpf 的 bin
        var here = AppContext.BaseDirectory;
        // 嘗試 net10.0-windows
        var candidates = new[]
        {
            Path.Combine(here, "..", "..", "..", "..", "..", "src", "PetSalon.Wpf", "bin", "Debug", "net10.0-windows", "PetSalon.Wpf.exe"),
            Path.Combine(here, "..", "..", "..", "..", "..", "src", "PetSalon.Wpf", "bin", "Release", "net10.0-windows", "PetSalon.Wpf.exe"),
        };
        foreach (var c in candidates)
        {
            var resolved = Path.GetFullPath(c);
            if (File.Exists(resolved)) return resolved;
        }
        throw new FileNotFoundException("找不到 PetSalon.Wpf.exe — 請先 dotnet build src\\PetSalon.Wpf");
    }

    private void WaitForFrameRendered()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (MainWindow.FindFirstDescendant(c => c.ByAutomationId("nav-home")) is not null) return;
            Thread.Sleep(100);
        }
    }

    public AutomationElement? FindById(string automationId)
        => MainWindow.FindFirstDescendant(c => c.ByAutomationId(automationId));

    public AutomationElement WaitForId(string automationId, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var el = FindById(automationId);
            if (el is not null) return el;
            Thread.Sleep(75);
        }
        throw new TimeoutException($"AutomationId '{automationId}' 在 {timeoutMs}ms 內未出現");
    }

    public void Dispose()
    {
        try { App.Close(); App.Dispose(); } catch { }
        Automation.Dispose();
        try { if (Directory.Exists(AppDataDir)) Directory.Delete(AppDataDir, recursive: true); } catch { }
    }
}
