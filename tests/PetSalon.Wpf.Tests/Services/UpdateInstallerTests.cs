using System.IO;
using FluentAssertions;
using PetSalon.Wpf.Services;
using Xunit;

namespace PetSalon.Wpf.Tests.Services;

/// <summary>
/// 自動更新會直接執行下載回來的 MSI，因此雜湊解析與比對是安全關鍵路徑 — 這裡把它釘死。
/// 實際的 HTTP 下載不在單元測試範圍。
/// </summary>
public sealed class UpdateInstallerTests
{
    private const string ValidHash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Fact]
    public void ParseSha256_accepts_bare_hash()
        => UpdateInstaller.ParseSha256(ValidHash).Should().Be(ValidHash);

    [Fact]
    public void ParseSha256_accepts_sha256sum_format()
        => UpdateInstaller.ParseSha256($"{ValidHash}  PetSalon-Setup-v1.2.3.msi")
            .Should().Be(ValidHash);

    [Fact]
    public void ParseSha256_is_case_insensitive_and_trims()
        => UpdateInstaller.ParseSha256($"  {ValidHash.ToUpperInvariant()}  \n")
            .Should().Be(ValidHash);

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("9f86d081")]
    [InlineData(ValidHash + "ab")]
    [InlineData("zzzzd081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08")]
    [InlineData("<html>404 Not Found</html>")]
    public void ParseSha256_rejects_anything_that_is_not_a_sha256(string content)
        => FluentActions.Invoking(() => UpdateInstaller.ParseSha256(content))
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public async Task ComputeSha256Async_matches_known_digest()
    {
        var path = Path.Combine(Path.GetTempPath(), $"petsalon-hash-{Guid.NewGuid():N}.bin");
        await File.WriteAllTextAsync(path, "test");   // SHA256("test") == ValidHash
        try
        {
            (await UpdateInstaller.ComputeSha256Async(path, default)).Should().Be(ValidHash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ComputeSha256Async_detects_a_single_changed_byte()
    {
        var path = Path.Combine(Path.GetTempPath(), $"petsalon-hash-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        var before = await UpdateInstaller.ComputeSha256Async(path, default);
        await File.WriteAllBytesAsync(path, [1, 2, 3, 5]);
        try
        {
            (await UpdateInstaller.ComputeSha256Async(path, default)).Should().NotBe(before);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DownloadVerifiedAsync_refuses_when_release_has_no_checksum()
    {
        // 沒有 sidecar 就不該冒險自動執行 — 呼叫端會退回手動下載
        var info = new UpdateInfo(
            new Version(9, 9, 9),
            "https://example.invalid/PetSalon-Setup-v9.9.9.msi",
            Sha256Url: null,
            "https://example.invalid/releases/tag/v9.9.9",
            "");

        info.CanAutoInstall.Should().BeFalse();
        await FluentActions.Awaiting(() => new UpdateInstaller().DownloadVerifiedAsync(info))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void CanAutoInstall_requires_both_msi_and_checksum()
    {
        Info(null, null).CanAutoInstall.Should().BeFalse();
        Info("https://x.invalid/a.msi", null).CanAutoInstall.Should().BeFalse();
        Info(null, "https://x.invalid/a.msi.sha256").CanAutoInstall.Should().BeFalse();
        Info("https://x.invalid/a.msi", "https://x.invalid/a.msi.sha256").CanAutoInstall.Should().BeTrue();

        static UpdateInfo Info(string? msi, string? sha)
            => new(new Version(1, 0, 0), msi, sha, "https://x.invalid/releases", "");
    }
}
