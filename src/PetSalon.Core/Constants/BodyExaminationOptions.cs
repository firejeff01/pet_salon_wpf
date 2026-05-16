namespace PetSalon.Core.Constants;

public static class BodyPart
{
    public const string Eyes = "eyes";
    public const string Ears = "ears";
    public const string Teeth = "teeth";
    public const string Limbs = "limbs";
    public const string Skin = "skin";
    public const string Fur = "fur";

    public static readonly IReadOnlyList<string> All = new[] { Eyes, Ears, Teeth, Limbs, Skin, Fur };

    public static string Label(string part) => part switch
    {
        Eyes => "眼睛",
        Ears => "耳朵",
        Teeth => "牙齒",
        Limbs => "四肢",
        Skin => "皮膚",
        Fur => "皮毛",
        _ => part,
    };
}

public static class BodyConditionOptions
{
    public static readonly IReadOnlyList<string> Standard = new[] { "正常", "異常" };
    public static readonly IReadOnlyList<string> Fur = new[] { "正常", "異常", "打結", "跳蚤壁蝨" };

    public static IReadOnlyList<string> For(string part)
        => part == BodyPart.Fur ? Fur : Standard;
}
