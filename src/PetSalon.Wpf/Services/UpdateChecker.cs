using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace PetSalon.Wpf.Services;

/// <param name="Latest">Release tag 解析出的版本。</param>
/// <param name="MsiUrl">MSI 資產的直接下載網址；release 沒有 MSI 資產時為 null。</param>
/// <param name="Sha256Url">MSI 對應的 .sha256 sidecar 網址；沒有 sidecar 時為 null（此時不做自動安裝）。</param>
/// <param name="ReleasePageUrl">手動下載用的 release 頁面，自動更新失敗時的退路。</param>
public sealed record UpdateInfo(
    Version Latest,
    string? MsiUrl,
    string? Sha256Url,
    string ReleasePageUrl,
    string ReleaseNotes)
{
    /// <summary>兩個網址齊備才可能走「下載 → 驗證雜湊 → 靜默安裝」流程。</summary>
    public bool CanAutoInstall => !string.IsNullOrWhiteSpace(MsiUrl) && !string.IsNullOrWhiteSpace(Sha256Url);
}

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

            // 取第一個 .msi 資產，以及同名的 .msi.sha256 sidecar
            string? msiName = null;
            string? msiUrl = null;
            string? shaUrl = null;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is null || !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) continue;
                if (!a.TryGetProperty("browser_download_url", out var u)) continue;
                msiName = name;
                msiUrl = u.GetString();
                break;
            }
            if (msiName is not null)
            {
                var wanted = msiName + ".sha256";
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)) continue;
                    if (a.TryGetProperty("browser_download_url", out var u)) shaUrl = u.GetString();
                    break;
                }
            }

            var pageUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/tag/{tag}";
            return new UpdateInfo(latest, msiUrl, shaUrl, pageUrl, body);
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
