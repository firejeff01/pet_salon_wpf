namespace PetSalon.Core.Constants;

/// <summary>對齊原 pet_salon 契約模板（contract.hbs）的 8 個個性選項，順序為模板排版順序。</summary>
public static class PersonalityOptions
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "親人", "親狗",
        "不會咬人", "會咬人",
        "不會咬狗貓", "會咬狗貓",
        "容易緊張", "有攻擊性",
    };

    public static bool IsValid(string item) => All.Contains(item);
}
