using FluentAssertions;
using PetSalon.Core.Common;
using Xunit;

namespace PetSalon.Core.Tests.Common;

public class RocDateTests
{
    [Theory]
    [InlineData(2024, 1, 15, "民國 113 年 1 月 15 日")]
    [InlineData(2026, 5, 16, "民國 115 年 5 月 16 日")]
    [InlineData(2011, 12, 31, "民國 100 年 12 月 31 日")]
    [InlineData(1912, 1, 1, "民國 1 年 1 月 1 日")]
    public void ToRocString_returns_民國_year(int y, int m, int d, string expected)
    {
        RocDate.ToRocString(new DateOnly(y, m, d)).Should().Be(expected);
    }

    [Fact]
    public void ToRocString_民國元年邊界_1912()
    {
        RocDate.ToRocString(new DateOnly(1912, 1, 1)).Should().Be("民國 1 年 1 月 1 日");
    }

    [Fact]
    public void ToRocString_民國前邊界_1911_returns_zero()
    {
        // 1911 = 民國 0 年（清朝末年/民國成立前）
        RocDate.ToRocString(new DateOnly(1911, 1, 1)).Should().Be("民國 0 年 1 月 1 日");
    }

    [Theory]
    [InlineData(2024, 1, 15, "113/01/15")]
    [InlineData(2024, 12, 31, "113/12/31")]
    [InlineData(2026, 5, 6, "115/05/06")]
    public void ToRocCompact_pads_to_two_digits(int y, int m, int d, string expected)
    {
        RocDate.ToRocCompact(new DateOnly(y, m, d)).Should().Be(expected);
    }

    [Fact]
    public void ToRocString_DateTimeOffset_converts_to_local_date()
    {
        var dt = new DateTimeOffset(2024, 7, 4, 13, 30, 0, TimeSpan.FromHours(8));
        RocDate.ToRocString(dt).Should().Be("民國 113 年 7 月 4 日");
    }
}
