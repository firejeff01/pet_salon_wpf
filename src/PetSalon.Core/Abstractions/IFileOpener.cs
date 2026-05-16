namespace PetSalon.Core.Abstractions;

public interface IFileOpener
{
    Task OpenAsync(string absolutePath, CancellationToken ct = default);
}
