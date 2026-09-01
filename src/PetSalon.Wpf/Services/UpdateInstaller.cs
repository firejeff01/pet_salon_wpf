using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PetSalon.Wpf.Services;

/// <summary>
/// 由 App 自行下載並安裝新版 MSI。
///
/// 這樣做的關鍵理由：Mark of the Web 是由瀏覽器／郵件軟體等「知道自己在從網路取檔」的程式
/// 主動蓋上的，HttpClient 寫出來的檔案不帶此標記，因此 msiexec 執行時不會觸發 SmartScreen
/// 的「Windows 已保護您的電腦」提示。使用者只需按一次「立即更新」。
///
/// 代價是我們變成自動執行下載回來的檔案，所以「執行前一定要比對 SHA256」不是可選項。
/// 校驗檔缺席或對不上就整個放棄，退回請使用者手動下載。
/// </summary>
public sealed class UpdateInstaller
{
    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    /// <summary>下載 MSI 與 sidecar、比對 SHA256，回傳可執行的本機路徑。</summary>
    /// <exception cref="InvalidOperationException">校驗失敗或缺少必要資訊。</exception>
    public async Task<string> DownloadVerifiedAsync(
        UpdateInfo info,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (!info.CanAutoInstall)
            throw new InvalidOperationException("這個版本沒有提供可自動安裝的 MSI 或校驗檔");

        var dir = Path.Combine(Path.GetTempPath(), "petsalon-update");
        CleanStaleDownloads(dir);
        Directory.CreateDirectory(dir);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"PetSalon-Wpf/{UpdateChecker.CurrentVersion()}");

        var expected = ParseSha256(await http.GetStringAsync(info.Sha256Url!, ct));

        var fileName = GetFileName(info.MsiUrl!) ?? $"PetSalon-Setup-v{info.Latest}.msi";
        var target = Path.Combine(dir, $"{Guid.NewGuid():N}-{fileName}");
        try
        {
            await DownloadAsync(http, info.MsiUrl!, target, progress, ct);

            var actual = await ComputeSha256Async(target, ct);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"下載檔案的 SHA256 與官方校驗值不符，已放棄安裝。\n預期：{expected}\n實際：{actual}");

            return target;
        }
        catch
        {
            TryDelete(target);
            throw;
        }
    }

    /// <summary>
    /// 產生一支批次檔並啟動它，由它稍候片刻後以 per-user 模式跑 msiexec（不需 UAC）。
    ///
    /// 中間這段延遲是必要的：App 必須先完全退出，否則 Windows Installer 會偵測到
    /// PetSalon.Wpf.exe 仍占用檔案而跳出「使用中的檔案」對話框，安裝就卡住了。
    /// 呼叫端應在此之後立刻關閉 App。
    /// </summary>
    public Process Launch(string msiPath, int delaySeconds = 3)
    {
        var script = Path.Combine(Path.GetDirectoryName(msiPath)!, $"{Guid.NewGuid():N}.cmd");

        // 腳本內容刻意全部維持 ASCII，MSI 路徑改用參數傳入（%~1）。
        // 批次檔是照主控台字碼頁解讀的，若把使用者路徑寫進檔案內容，
        // 遇到中文使用者名稱就會變成亂碼；而命令列參數走的是 Unicode，不受影響。
        // ping 當延遲用 — timeout 指令在沒有主控台的情況下會直接失敗。
        // 裝完順手清掉 MSI 與這支腳本自己，避免 temp 累積。
        File.WriteAllText(script, string.Join(Environment.NewLine,
        [
            "@echo off",
            $"ping -n {Math.Max(delaySeconds, 1) + 1} 127.0.0.1 >nul",
            "msiexec /i \"%~1\" /qb /norestart",
            "del \"%~1\" >nul 2>&1",
            "del \"%~f0\" >nul 2>&1",
            "",
        ]), System.Text.Encoding.ASCII);

        var psi = new ProcessStartInfo(script)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath(),
        };
        psi.ArgumentList.Add(msiPath);
        return Process.Start(psi) ?? throw new InvalidOperationException("無法啟動安裝程式");
    }

    private static async Task DownloadAsync(
        HttpClient http,
        string url,
        string target,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total is > 0) progress?.Report((double)read / total.Value);
        }
    }

    internal static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>接受純雜湊或 sha256sum 格式（"&lt;hash&gt;  &lt;filename&gt;"）。</summary>
    internal static string ParseSha256(string content)
    {
        var first = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
        var token = first.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        token = token.Trim().ToLowerInvariant();
        if (!Sha256Pattern.IsMatch(token))
            throw new InvalidOperationException("校驗檔格式不正確，已放棄安裝");
        return token;
    }

    private static string? GetFileName(string url)
    {
        var name = Path.GetFileName(new Uri(url).AbsolutePath);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>清掉上次留下的下載（安裝成功後 App 已關閉，沒有機會自己刪）。</summary>
    private static void CleanStaleDownloads(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            var cutoff = DateTime.UtcNow.AddDays(-1);
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                if (File.GetLastWriteTimeUtc(f) < cutoff) TryDelete(f);
            }
        }
        catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
