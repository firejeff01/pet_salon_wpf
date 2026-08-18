namespace PetSalon.Core.Dtos;

public sealed record ShopSignatureProfile(
    string SignatureId,
    string Name,
    string ImageFileName,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
