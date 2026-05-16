using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace PetSalon.Wpf.Services;

public sealed record UpdateInfo(Version Latest, string DownloadUrl, string ReleaseNotes);

/// <summary>
/// 啟動時呼叫 GitHub Releases API 偵測新版本。網路或 API 失敗皆 silent — 不阻擋啟動。
/// </summary>
public sealed class UpdateChecker
{
    private const string GitHubOwner = "firejeff01";
    private const string GitHubRepo = "pet_salon_wpf";
    private static readonly Uri ReleasesApi = new($"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest");

    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"PetSalon-Wpf/{CurrentVersion()}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var resp = await http.GetAsync(ReleasesApi, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var body = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";
            var assets = root.GetProperty("assets");

            // tag 形式：v1.2.0 → 取 1.2.0；非標準格式回 null
            var tagVer = tag.TrimStart('v', 'V');
            if (!Version.TryParse(tagVer, out var latest)) return null;

            var current = CurrentVersion();
            if (latest <= current) return null;

            // 取第一個 .msi 資產的 browser_download_url
            string? msiUrl = null;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is null || !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) continue;
                if (a.TryGetProperty("browser_download_url", out var u)) { msiUrl = u.GetString(); break; }
            }
            // 沒 MSI 資產 → 退回 release 頁面
            msiUrl ??= $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/tag/{tag}";
            return new UpdateInfo(latest, msiUrl, body);
        }
        catch
        {
            return null;
        }
    }

    public static Version CurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        // 標準化為 3-part 比對（忽略 build/revision）
        return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
    }
}
