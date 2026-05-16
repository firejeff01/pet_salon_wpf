using System.Text.RegularExpressions;

namespace PetSalon.Core.Common;

public static partial class FileNameSanitizer
{
    [GeneratedRegex("[\\\\/:*?\"<>|]")]
    private static partial Regex IllegalCharsRegex();

    [GeneratedRegex("_{2,}")]
    private static partial Regex MultipleUnderscoresRegex();

    public static string Sanitize(string name)
    {
        var trimmed = name.Trim();
        var replaced = IllegalCharsRegex().Replace(trimmed, "_");
        var compacted = MultipleUnderscoresRegex().Replace(replaced, "_");
        return compacted.Trim('_');
    }

    public static string BuildPdfFileName(DateOnly date, string ownerName, string petName)
    {
        return $"{date:yyyy-MM-dd}_{Sanitize(ownerName)}_{Sanitize(petName)}.pdf";
    }
}
