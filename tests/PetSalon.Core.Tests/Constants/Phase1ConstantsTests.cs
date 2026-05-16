using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentAssertions;
using PetSalon.Core.Constants;
using PetSalon.Core.Entities;
using Xunit;

namespace PetSalon.Core.Tests.Constants;

/// <summary>
/// Phase 1 單元測試：
///   1.1 PhysicalExamination 6 個 *Note 欄位
///   1.2 BodyConditionOptions.Fur 擴充含「異常」
///   1.3 MedicalHistoryOptions.All 第 19 項「以上皆無」
/// </summary>
public class Phase1ConstantsTests
{
    // ===== 1.1 PhysicalExamination *Note 欄位 =====

    [Theory(DisplayName = "1.1 / PhysicalExamination 應含 6 個 *Note : string? 屬性")]
    [InlineData("EyesNote")]
    [InlineData("EarsNote")]
    [InlineData("TeethNote")]
    [InlineData("LimbsNote")]
    [InlineData("SkinNote")]
    [InlineData("FurNote")]
    public void PhysicalExamination_has_note_property(string propName)
    {
        var prop = typeof(PhysicalExamination)
            .GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);

        prop.Should().NotBeNull($"PhysicalExamination 應含 {propName} 屬性");
        // 型別應為 string?（Nullable<string> 在 reflection 中仍是 typeof(string)）
        prop!.PropertyType.Should().Be(typeof(string), $"{propName} 型別應為 string?");
    }

    [Theory(DisplayName = "1.1 / *Note 屬性預設值應為 null")]
    [InlineData("EyesNote")]
    [InlineData("EarsNote")]
    [InlineData("TeethNote")]
    [InlineData("LimbsNote")]
    [InlineData("SkinNote")]
    [InlineData("FurNote")]
    public void PhysicalExamination_note_defaults_to_null(string propName)
    {
        var exam = new PhysicalExamination();
        var prop = typeof(PhysicalExamination)
            .GetProperty(propName, BindingFlags.Public | BindingFlags.Instance)!;

        var value = prop.GetValue(exam);
        value.Should().BeNull($"{propName} 預設值應為 null（R6/Q5）");
    }

    [Theory(DisplayName = "1.1 / *Note 屬性具有 MaxLength(30) DataAnnotation")]
    [InlineData("EyesNote")]
    [InlineData("EarsNote")]
    [InlineData("TeethNote")]
    [InlineData("LimbsNote")]
    [InlineData("SkinNote")]
    [InlineData("FurNote")]
    public void PhysicalExamination_note_has_max_length_30(string propName)
    {
        var prop = typeof(PhysicalExamination)
            .GetProperty(propName, BindingFlags.Public | BindingFlags.Instance)!;

        var attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        attr.Should().NotBeNull($"{propName} 應有 [MaxLength] 屬性（R6/Q7 30 字上限）");
        attr!.Length.Should().Be(30, $"{propName} 最大長度應為 30");
    }

    // ===== 1.2 BodyConditionOptions.Fur 擴充 =====

    [Fact(DisplayName = "1.2 / BodyConditionOptions.Fur 應包含「異常」")]
    public void BodyConditionOptions_Fur_contains_abnormal()
    {
        BodyConditionOptions.Fur.Should().Contain("異常", "皮毛選項應新增「異常」（R6/Q8）");
    }

    [Fact(DisplayName = "1.2 / BodyConditionOptions.Fur 順序應為 正常→異常→打結→跳蚤壁蝨")]
    public void BodyConditionOptions_Fur_order_is_correct()
    {
        var expected = new[] { "正常", "異常", "打結", "跳蚤壁蝨" };
        BodyConditionOptions.Fur.Should().Equal(expected, "皮毛選項順序應為正常、異常、打結、跳蚤壁蝨（R6/Q8）");
    }

    [Fact(DisplayName = "1.2 / BodyConditionOptions.Fur 共 4 項")]
    public void BodyConditionOptions_Fur_has_4_items()
    {
        BodyConditionOptions.Fur.Count.Should().Be(4, "皮毛選項應擴充至 4 項");
    }

    [Fact(DisplayName = "1.2 / BodyConditionOptions.Standard 不應受影響（仍為 2 項）")]
    public void BodyConditionOptions_Standard_unchanged()
    {
        BodyConditionOptions.Standard.Should().Equal(new[] { "正常", "異常" });
    }

    // ===== 1.3 MedicalHistoryOptions.All 新增「以上皆無」=====

    [Fact(DisplayName = "1.3 / MedicalHistoryOptions.All 擴充為 19 項")]
    public void MedicalHistoryOptions_All_has_19_items()
    {
        MedicalHistoryOptions.All.Count.Should().Be(19, "應新增「以上皆無」至第 19 項（R7/Q6）");
    }

    [Fact(DisplayName = "1.3 / MedicalHistoryOptions.All 末尾（index 18）為「以上皆無」")]
    public void MedicalHistoryOptions_All_last_item_is_none_of_above()
    {
        MedicalHistoryOptions.All[18].Should().Be("以上皆無", "第 19 項（index 18）應為「以上皆無」");
    }

    [Fact(DisplayName = "1.3 / IsValid(\"以上皆無\") 應回傳 true")]
    public void MedicalHistoryOptions_IsValid_none_of_above_returns_true()
    {
        MedicalHistoryOptions.IsValid("以上皆無").Should().BeTrue();
    }

    [Fact(DisplayName = "1.3 / 前 18 項順序與字串內容皆不變")]
    public void MedicalHistoryOptions_first_18_items_unchanged()
    {
        var expected = new[]
        {
            "心臟病", "氣喘", "氣管塌陷", "癲癇",
            "白內障", "心絲蟲", "艾利希體",
            "腸炎", "腹膜炎", "腹積水",
            "血便", "血尿", "骨折", "髖關節問題",
            "懷孕", "手術外傷未癒合",
            "傳染性疾病", "其它",
        };

        for (int i = 0; i < expected.Length; i++)
        {
            MedicalHistoryOptions.All[i].Should().Be(expected[i], $"index {i} 不應變更");
        }
    }
}
