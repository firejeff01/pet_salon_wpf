using FluentAssertions;
using Xunit;

namespace PetSalon.Wpf.Tests.Sanity;

public class WpfTestSanityTests
{
    [Fact]
    public void Wpf_test_runner_works()
    {
        "ok".Should().Be("ok");
    }
}
