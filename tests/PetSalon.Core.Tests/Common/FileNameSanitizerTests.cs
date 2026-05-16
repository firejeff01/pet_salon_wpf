using FluentAssertions;
using PetSalon.Core.Common;
using Xunit;

namespace PetSalon.Core.Tests.Common;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("John", "John")]
    [InlineData("張三", "張三")]
    [InlineData("  spaced  ", "spaced")]
    [InlineData("a/b\\c", "a_b_c")]
    [InlineData("a:b*c?d\"e<f>g|h", "a_b_c_d_e_f_g_h")]
    [InlineData("multi___underscores", "multi_underscores")]
    [InlineData("_lead_trail_", "lead_trail")]
    [InlineData("", "")]
    [InlineData("///", "")]
    [InlineData("a//b", "a_b")]
    public void Sanitize_strips_illegal_chars_and_compacts(string input, string expected)
    {
        FileNameSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void BuildPdfFileName_concatenates_date_owner_pet_pdf()
    {
        var date = new DateOnly(2024, 1, 15);
        FileNameSanitizer.BuildPdfFileName(date, "張三", "毛毛").Should().Be("2024-01-15_張三_毛毛.pdf");
    }

    [Fact]
    public void BuildPdfFileName_sanitizes_name_components()
    {
        var date = new DateOnly(2024, 1, 15);
        FileNameSanitizer.BuildPdfFileName(date, "John/Doe", "Pet*Name").Should().Be("2024-01-15_John_Doe_Pet_Name.pdf");
    }
}
