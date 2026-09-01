using FluentAssertions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
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
    public async Task Each_role_keeps_its_own_single_default()
    {
        var groomerA = await _svc.CreateAsync("小美", SignatureRole.Groomer, ValidPng(), makeDefault: true);
        var groomerB = await _svc.CreateAsync("小華", SignatureRole.Groomer, ValidPng());
        var manager = await _svc.CreateAsync("店長", SignatureRole.Manager, ValidPng());

        await _svc.SetDefaultAsync(groomerB.SignatureId);

        var profiles = await _svc.ListAsync();
        profiles.Should().HaveCount(3);
        profiles.Single(x => x.Role == SignatureRole.Groomer && x.IsDefault)
            .SignatureId.Should().Be(groomerB.SignatureId);
        // 切換美容人員預設不應影響負責人的預設。
        profiles.Single(x => x.Role == SignatureRole.Manager && x.IsDefault)
            .SignatureId.Should().Be(manager.SignatureId);
        (await _svc.ReadPngAsync(groomerA.SignatureId)).Should().Equal(ValidPng());
    }

    [Fact]
    public async Task First_profile_of_each_role_becomes_that_roles_default()
    {
        var groomer = await _svc.CreateAsync("小美", SignatureRole.Groomer, ValidPng());
        var manager = await _svc.CreateAsync("店長", SignatureRole.Manager, ValidPng());

        groomer.IsDefault.Should().BeTrue();
        manager.IsDefault.Should().BeTrue();
        (await _svc.GetDefaultAsync(SignatureRole.Groomer))!.SignatureId.Should().Be(groomer.SignatureId);
        (await _svc.GetDefaultAsync(SignatureRole.Manager))!.SignatureId.Should().Be(manager.SignatureId);
    }

    [Fact]
    public async Task ListAsync_by_role_returns_only_that_role()
    {
        await _svc.CreateAsync("小美", SignatureRole.Groomer, ValidPng());
        await _svc.CreateAsync("小華", SignatureRole.Groomer, ValidPng());
        await _svc.CreateAsync("店長", SignatureRole.Manager, ValidPng());

        (await _svc.ListAsync(SignatureRole.Groomer)).Should().HaveCount(2);
        (await _svc.ListAsync(SignatureRole.Manager)).Should().ContainSingle()
            .Which.Name.Should().Be("店長");
    }

    [Fact]
    public async Task Changing_role_moves_the_profile_and_repairs_both_role_defaults()
    {
        var groomerA = await _svc.CreateAsync("小美", SignatureRole.Groomer, ValidPng());
        var groomerB = await _svc.CreateAsync("小華", SignatureRole.Groomer, ValidPng());

        // groomerA 是美容人員的預設，改成負責人後：
        // 負責人第一組 → 成為負責人預設；美容人員預設遞補給 groomerB。
        await _svc.ChangeRoleAsync(groomerA.SignatureId, SignatureRole.Manager);

        var profiles = await _svc.ListAsync();
        var moved = profiles.Single(x => x.SignatureId == groomerA.SignatureId);
        moved.Role.Should().Be(SignatureRole.Manager);
        moved.IsDefault.Should().BeTrue();
        profiles.Single(x => x.SignatureId == groomerB.SignatureId).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_default_promotes_another_profile_of_the_same_role_only()
    {
        var groomerA = await _svc.CreateAsync("小美", SignatureRole.Groomer, ValidPng());
        var groomerB = await _svc.CreateAsync("小華", SignatureRole.Groomer, ValidPng());
        var manager = await _svc.CreateAsync("店長", SignatureRole.Manager, ValidPng());

        await _svc.DeleteAsync(groomerA.SignatureId);

        var profiles = await _svc.ListAsync();
        profiles.Should().HaveCount(2);
        profiles.Single(x => x.SignatureId == groomerB.SignatureId).IsDefault.Should().BeTrue();
        profiles.Single(x => x.SignatureId == manager.SignatureId).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_the_last_profile_of_a_role_leaves_that_role_without_default()
    {
        var manager = await _svc.CreateAsync("店長", SignatureRole.Manager, ValidPng());
        await _svc.CreateAsync("小美", SignatureRole.Groomer, ValidPng());

        await _svc.DeleteAsync(manager.SignatureId);

        (await _svc.GetDefaultAsync(SignatureRole.Manager)).Should().BeNull();
        (await _svc.GetDefaultAsync(SignatureRole.Groomer)).Should().NotBeNull();
    }

    [Fact]
    public async Task Legacy_profiles_without_role_are_read_as_groomer()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(Path.Combine(_root, "sig_legacy.png"), ValidPng());
        await File.WriteAllTextAsync(Path.Combine(_root, "profiles.json"), """
        [
          {
            "signatureId": "sig_legacy",
            "name": "舊簽名",
            "imageFileName": "sig_legacy.png",
            "isDefault": true,
            "createdAt": "2026-08-18T00:00:00+08:00",
            "updatedAt": "2026-08-18T00:00:00+08:00"
          }
        ]
        """);

        var profile = (await _svc.ListAsync()).Should().ContainSingle().Subject;
        profile.Role.Should().Be(SignatureRole.Groomer);
        profile.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Missing_image_is_reported_as_signature_unavailable()
    {
        var profile = await _svc.CreateAsync("店長", SignatureRole.Manager, ValidPng());
        File.Delete(Path.Combine(_root, profile.ImageFileName));

        var exception = await FluentActions.Awaiting(() => _svc.ReadPngAsync(profile.SignatureId))
            .Should().ThrowAsync<AppException>();
        exception.Which.Code.Should().Be("SIGNATURE_UNAVAILABLE");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
