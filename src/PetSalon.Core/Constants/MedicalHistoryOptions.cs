namespace PetSalon.Core.Constants;

/// <summary>對齊原 pet_salon 契約模板（contract.hbs）的 18 個病史選項，空陣列代表「無」。</summary>
public static class MedicalHistoryOptions
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "心臟病", "氣喘", "氣管塌陷", "癲癇",
        "白內障", "心絲蟲", "艾利希體",
        "腸炎", "腹膜炎", "腹積水",
        "血便", "血尿", "骨折", "髖關節問題",
        "懷孕", "手術外傷未癒合",
        "傳染性疾病", "其它",
        "以上皆無",
    };

    public static bool IsValid(string item) => All.Contains(item);
}
