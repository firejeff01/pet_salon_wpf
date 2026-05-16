using System.Diagnostics;
using PetSalon.Core.Abstractions;

namespace PetSalon.Infrastructure.Os;

public sealed class WindowsFileOpener : IFileOpener
{
    public Task OpenAsync(string absolutePath, CancellationToken ct = default)
    {
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("找不到要開啟的檔案", absolutePath);

        var psi = new ProcessStartInfo
        {
            FileName = absolutePath,
            UseShellExecute = true,
        };
        Process.Start(psi);
        return Task.CompletedTask;
    }
}
