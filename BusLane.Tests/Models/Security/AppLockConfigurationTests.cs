namespace BusLane.Tests.Models.Security;

using BusLane.Models.Security;
using FluentAssertions;

public sealed class AppLockConfigurationTests
{
    [Fact]
    public void ToString_ShouldRedactPassword()
    {
        // Arrange
        var configuration = new AppLockConfiguration("Secret#1", true);

        // Act
        var result = configuration.ToString();

        // Assert
        result.Should().NotContain("Secret#1");
        result.Should().Contain("<redacted>");
        result.Should().Contain("BiometricUnlockEnabled = True");
    }
}
