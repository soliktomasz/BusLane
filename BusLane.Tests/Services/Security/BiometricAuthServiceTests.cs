namespace BusLane.Tests.Services.Security;

using BusLane.Models.Security;
using BusLane.Services.Security;
using FluentAssertions;

public sealed class BiometricAuthServiceTests
{
    [Theory]
    [InlineData(NativeBiometricAvailability.Available, BiometricAvailability.Available)]
    [InlineData(NativeBiometricAvailability.NotEnrolled, BiometricAvailability.Unavailable)]
    [InlineData(NativeBiometricAvailability.Unavailable, BiometricAvailability.Unavailable)]
    public async Task PlatformBiometricAuthService_GetAvailabilityAsync_MapsNativeAvailability(
        NativeBiometricAvailability nativeAvailability,
        BiometricAvailability expectedAvailability)
    {
        // Arrange
        var adapter = new FakeBiometricPromptAdapter
        {
            Availability = nativeAvailability
        };
        var sut = new TestPlatformBiometricAuthService(adapter);

        // Act
        var result = await sut.GetAvailabilityAsync();

        // Assert
        result.Should().Be(expectedAvailability);
    }

    [Theory]
    [InlineData(NativeBiometricPromptResult.Success, BiometricAuthResult.Success)]
    [InlineData(NativeBiometricPromptResult.Cancelled, BiometricAuthResult.Cancelled)]
    [InlineData(NativeBiometricPromptResult.Failed, BiometricAuthResult.Failed)]
    [InlineData(NativeBiometricPromptResult.Unavailable, BiometricAuthResult.Unavailable)]
    public async Task PlatformBiometricAuthService_AuthenticateAsync_MapsNativeResult(
        NativeBiometricPromptResult nativeResult,
        BiometricAuthResult expectedResult)
    {
        // Arrange
        var adapter = new FakeBiometricPromptAdapter
        {
            AuthenticateResult = nativeResult
        };
        var sut = new TestPlatformBiometricAuthService(adapter);

        // Act
        var result = await sut.AuthenticateAsync("Unlock BusLane");

        // Assert
        result.Should().Be(expectedResult);
        adapter.LastReason.Should().Be("Unlock BusLane");
    }

    [Fact]
    public async Task NoOpBiometricAuthService_ShouldAlwaysReturnUnavailable()
    {
        // Arrange
        var sut = new NoOpBiometricAuthService();

        // Act
        var availability = await sut.GetAvailabilityAsync();
        var result = await sut.AuthenticateAsync("Unlock BusLane");

        // Assert
        availability.Should().Be(BiometricAvailability.Unavailable);
        result.Should().Be(BiometricAuthResult.Unavailable);
    }

    [Fact]
    public async Task NoOpBiometricAuthService_AuthenticateAsync_WithBlankReason_ShouldThrow()
    {
        // Arrange
        var sut = new NoOpBiometricAuthService();

        // Act
        Func<Task> act = () => sut.AuthenticateAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class FakeBiometricPromptAdapter : IBiometricPromptAdapter
    {
        public NativeBiometricAvailability Availability { get; set; } = NativeBiometricAvailability.Unavailable;
        public NativeBiometricPromptResult AuthenticateResult { get; set; } = NativeBiometricPromptResult.Unavailable;
        public string? LastReason { get; private set; }

        public Task<NativeBiometricAvailability> GetAvailabilityAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Availability);
        }

        public Task<NativeBiometricPromptResult> AuthenticateAsync(string reason, CancellationToken ct = default)
        {
            LastReason = reason;
            return Task.FromResult(AuthenticateResult);
        }
    }

    private sealed class TestPlatformBiometricAuthService : PlatformBiometricAuthService
    {
        public TestPlatformBiometricAuthService(IBiometricPromptAdapter adapter)
            : base(adapter)
        {
        }
    }
}
