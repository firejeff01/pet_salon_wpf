namespace PetSalon.Core.Constants;

public static class ChipStatusOptions
{
    public const string Unspecified = "未填";
    public const string HasChip = "有";
    public const string NoChip = "無";

    public static readonly IReadOnlyList<string> All = new[] { Unspecified, HasChip, NoChip };
}
