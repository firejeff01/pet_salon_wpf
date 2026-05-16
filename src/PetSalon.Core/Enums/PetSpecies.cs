namespace PetSalon.Core.Enums;

public static class PetSpecies
{
    public const string Dog = "犬";
    public const string Cat = "貓";

    public static readonly IReadOnlyList<string> All = new[] { Dog, Cat };
}

public static class PetGender
{
    public const string Male = "公";
    public const string Female = "母";

    public static readonly IReadOnlyList<string> All = new[] { Male, Female };
}
