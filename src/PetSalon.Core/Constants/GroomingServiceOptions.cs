namespace PetSalon.Core.Constants;

public static class GroomingServiceOptions
{
    public const string Wash = "洗澡";
    public const string Groom = "美容";
    public const string Other = "其他";

    public static readonly IReadOnlyList<string> All = new[] { Wash, Groom, Other };

    public static bool IsValid(string item) => All.Contains(item);
}
