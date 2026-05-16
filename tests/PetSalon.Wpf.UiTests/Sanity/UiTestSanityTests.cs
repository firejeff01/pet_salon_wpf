using FluentAssertions;
using Xunit;

namespace PetSalon.Wpf.UiTests.Sanity;

public class UiTestSanityTests
{
    [Fact]
    public void Ui_test_runner_works()
    {
        // 真正的 FlaUI 啟動 + 拆解流程於後續 fixture 提供
        true.Should().BeTrue();
    }
}
