using FluentAssertions;
using PetSalon.Core.Services;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public class StoredValueServiceTests
{
    private readonly StoredValueService _svc = new();

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 100, 0, 100, 0)]            // 無餘額：全現金
    [InlineData(100, 0, 0, 0, 100)]            // 無消費：餘額不變
    [InlineData(100, 100, 100, 0, 0)]          // 完全扣抵
    [InlineData(50, 100, 50, 50, 0)]           // 餘額不足：扣抵 + 補現金
    [InlineData(100, 50, 50, 0, 50)]           // 餘額有剩
    public void Calculate_handles_typical_cases(decimal balance, decimal cost,
        decimal expectedDeduction, decimal expectedCash, decimal expectedRemaining)
    {
        var r = _svc.Calculate(balance, cost);
        r.Deduction.Should().Be(expectedDeduction);
        r.Cash.Should().Be(expectedCash);
        r.Remaining.Should().Be(expectedRemaining);
    }

    [Fact]
    public void Negative_balance_treated_as_zero()
    {
        var r = _svc.Calculate(-50m, 100m);
        r.Deduction.Should().Be(0);
        r.Cash.Should().Be(100);
        r.Remaining.Should().Be(0);
    }

    [Fact]
    public void Negative_cost_treated_as_zero()
    {
        var r = _svc.Calculate(100m, -50m);
        r.Deduction.Should().Be(0);
        r.Cash.Should().Be(0);
        r.Remaining.Should().Be(100);
    }

    [Fact]
    public void Large_decimal_values_are_preserved()
    {
        var r = _svc.Calculate(12_345.67m, 9_999.99m);
        r.Deduction.Should().Be(9_999.99m);
        r.Cash.Should().Be(0);
        r.Remaining.Should().Be(2_345.68m);
    }
}
