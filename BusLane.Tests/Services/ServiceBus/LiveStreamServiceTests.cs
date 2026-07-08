namespace BusLane.Tests.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;

public class LiveStreamServiceTests
{
    private const string TestConnectionString =
        "Endpoint=sb://unit-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    [Fact]
    public async Task StartQueueStreamAsync_ReturnsImmediately_AndDoesNotReportStreamingBeforeFirstSuccessfulPeek()
    {
        // Arrange
        var preferences = Substitute.For<IPreferencesService>();
        preferences.LiveStreamPollingIntervalSeconds.Returns(1);

        await using var client = new ServiceBusClient(TestConnectionString);
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetClient().Returns(client);

        await using var sut = new LiveStreamService(preferences);

        // Act
        var startTask = sut.StartQueueStreamAsync(operations, "queue-a");
        var completedTask = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        completedTask.Should().Be(startTask, "startup should not wait for the streaming loop to finish");
        await startTask;
        sut.IsStreaming.Should().BeFalse("streaming status should only turn on after the entity answers a peek");

        await sut.StopStreamAsync();
    }

    [Fact]
    public void CreateStreamBody_WithLargePayload_ReturnsBoundedPreview()
    {
        // Arrange
        var payload = BinaryData.FromString(new string('a', 5000));

        // Act
        var result = LiveStreamService.CreateStreamBody(payload);

        // Assert
        result.Should().HaveLength(4099);
        result.Should().EndWith("...");
    }

    [Fact]
    public void CreateStreamBody_WhenLimitSplitsUtf8Character_DoesNotEmitReplacementCharacter()
    {
        // Arrange
        var payload = BinaryData.FromString($"{new string('a', 4095)}😀tail");

        // Act
        var result = LiveStreamService.CreateStreamBody(payload);

        // Assert
        result.Should().NotContain("\uFFFD");
        result.Should().EndWith("...");
    }
}
