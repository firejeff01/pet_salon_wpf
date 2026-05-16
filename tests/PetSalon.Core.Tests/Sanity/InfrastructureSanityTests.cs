using FluentAssertions;
using Xunit;

namespace PetSalon.Core.Tests.Sanity;

public class InfrastructureSanityTests
{
    [Fact]
    public void Test_framework_is_running()
    {
        (1 + 1).Should().Be(2);
    }
}
