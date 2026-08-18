using PetSalon.Core.Dtos;

namespace PetSalon.Core.Abstractions;

public interface IShopSignatureStore
{
    Task<IReadOnlyList<ShopSignatureProfile>> ListAsync(CancellationToken ct = default);
    Task CreateAsync(ShopSignatureProfile profile, byte[] pngBytes, CancellationToken ct = default);
    Task ReplaceProfilesAsync(IReadOnlyList<ShopSignatureProfile> profiles, CancellationToken ct = default);
    Task<byte[]> ReadPngAsync(ShopSignatureProfile profile, CancellationToken ct = default);
    Task DeleteAsync(
        ShopSignatureProfile profile,
        IReadOnlyList<ShopSignatureProfile> remainingProfiles,
        CancellationToken ct = default);
}
