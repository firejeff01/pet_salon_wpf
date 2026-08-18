using FluentAssertions;
using PetSalon.Core.Common;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using PetSalon.Infrastructure.Signatures;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public sealed class ShopSignatureServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "shop-signature-test-" + Guid.NewGuid().ToString("N"));
    private readonly ShopSignatureService _svc;

    public ShopSignatureServiceTests()
    {
        var options = new ShopSignatureOptions { RootDir = _root };
        _svc = new ShopSignatureService(
            new FileShopSignatureStore(options),
            new SequentialIdGenerator(),
            new FakeClock(),
            options);
    }

    private static byte[] ValidPng() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    [Fact]
    public async Task Profiles_support_multiple_entries_and_exactly_one_default()
    {
        var first = await _svc.CreateAsync("媽媽", ValidPng(), makeDefault: true);
        var second = await _svc.CreateAsync("店長", ValidPng());
        await _svc.SetDefaultAsync(second.SignatureId);

        var profiles = await _svc.ListAsync();
        profiles.Should().HaveCount(2);
        profiles.Single(x => x.IsDefault).SignatureId.Should().Be(second.SignatureId);
        (await _svc.ReadPngAsync(first.SignatureId)).Should().Equal(ValidPng());
    }

    [Fact]
    public async Task Missing_image_is_reported_as_signature_unavailable()
    {
        var profile = await _svc.CreateAsync("店長", ValidPng());
        File.Delete(Path.Combine(_root, profile.ImageFileName));

        var exception = await FluentActions.Awaiting(() => _svc.ReadPngAsync(profile.SignatureId))
            .Should().ThrowAsync<AppException>();
        exception.Which.Code.Should().Be("SIGNATURE_UNAVAILABLE");
    }

    [Fact]
    public async Task First_profile_becomes_default_and_deleting_default_promotes_another()
    {
        var first = await _svc.CreateAsync("媽媽", ValidPng());
        var second = await _svc.CreateAsync("店長", ValidPng());

        first.IsDefault.Should().BeTrue();
        await _svc.DeleteAsync(first.SignatureId);

        var remaining = (await _svc.ListAsync()).Should().ContainSingle().Subject;
        remaining.SignatureId.Should().Be(second.SignatureId);
        remaining.IsDefault.Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
