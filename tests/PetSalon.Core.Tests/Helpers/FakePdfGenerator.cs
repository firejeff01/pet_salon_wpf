using PetSalon.Core.Abstractions;

namespace PetSalon.Core.Tests.Helpers;

/// <summary>不真的產 PDF：只寫一個 byte，留下版本與 path 供斷言。</summary>
public sealed class FakePdfGenerator : IPdfGenerator
{
    public List<ContractRenderData> CapturedCalls { get; } = new();

    public Task<ContractGenerateOutput> GenerateContractAsync(
        ContractRenderData data,
        string outputDir,
        int nextVersion,
        CancellationToken ct = default)
    {
        CapturedCalls.Add(data);
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, $"fake_v{nextVersion}.pdf");
        File.WriteAllBytes(path, new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF magic
        return Task.FromResult(new ContractGenerateOutput(path, nextVersion));
    }
}

public sealed class NoopFileOpener : IFileOpener
{
    public List<string> Opened { get; } = new();
    public Task OpenAsync(string absolutePath, CancellationToken ct = default)
    {
        Opened.Add(absolutePath);
        return Task.CompletedTask;
    }
}
