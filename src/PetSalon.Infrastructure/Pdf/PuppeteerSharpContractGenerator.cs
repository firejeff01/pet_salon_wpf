using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace PetSalon.Infrastructure.Pdf;

/// <summary>
/// 用 PuppeteerSharp 啟動 Chromium 把 contract.hbs 渲染成 PDF。
/// Chromium 二進位在第一次啟動時下載到使用者 AppData (BrowserFetcher 預設位置)。
/// </summary>
public sealed class PuppeteerSharpContractGenerator : IPdfGenerator, IAsyncDisposable
{
    private readonly ContractTemplateRenderer _renderer;
    private readonly string _chromiumCacheDir;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _launchLock = new(1, 1);

    public PuppeteerSharpContractGenerator(string templatePath, string fontPath, string chromiumCacheDir)
    {
        _renderer = new ContractTemplateRenderer(templatePath, fontPath);
        _chromiumCacheDir = chromiumCacheDir;
        Directory.CreateDirectory(_chromiumCacheDir);
    }

    public async Task<ContractGenerateOutput> GenerateContractAsync(
        ContractRenderData data,
        string outputDir,
        int nextVersion,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);
        var fileName = BuildFileName(data, nextVersion);
        var absolutePath = Path.GetFullPath(Path.Combine(outputDir, fileName));

        var html = _renderer.Render(data);
        var browser = await EnsureBrowserAsync(ct);
        await using var page = await browser.NewPageAsync();
        try
        {
            await page.SetContentAsync(html, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 60000,
            });
            await page.EvaluateExpressionAsync("document.fonts && document.fonts.ready");
            await page.EmulateMediaTypeAsync(PuppeteerSharp.Media.MediaType.Screen);
            await page.PdfAsync(absolutePath, new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                PreferCSSPageSize = true,
            });
        }
        finally
        {
            await page.CloseAsync();
        }

        return new ContractGenerateOutput(absolutePath, nextVersion);
    }

    private async Task<IBrowser> EnsureBrowserAsync(CancellationToken ct)
    {
        if (_browser is not null && _browser.IsConnected) return _browser;
        await _launchLock.WaitAsync(ct);
        try
        {
            if (_browser is not null && _browser.IsConnected) return _browser;
            var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions
            {
                Path = _chromiumCacheDir,
                Browser = SupportedBrowser.Chrome,
            });
            var installed = await browserFetcher.DownloadAsync();
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = installed.GetExecutablePath(),
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" },
            });
            return _browser;
        }
        finally
        {
            _launchLock.Release();
        }
    }

    private static string BuildFileName(ContractRenderData data, int version)
    {
        var date = data.GroomingRecord.ServiceDate.ToString("yyyyMMdd");
        var time = data.GroomingRecord.ServiceTime.ToString("HHmm");
        var pet = FileNameSanitizer.Sanitize(data.Pet.Name);
        var owner = FileNameSanitizer.Sanitize(data.Owner.Name);
        return $"{date}_{time}_{pet}_{owner}_契約_v{version}.pdf";
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            try { await _browser.CloseAsync(); } catch { }
            await _browser.DisposeAsync();
            _browser = null;
        }
        _launchLock.Dispose();
    }
}
