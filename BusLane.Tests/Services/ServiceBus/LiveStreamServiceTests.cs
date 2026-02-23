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
    public async Task StartQueueStreamAsync_PeekMode_ReturnsImmediatelyAndSetsStreamingState()
    {
        // Arrange
        var preferences = Substitute.For<IPreferencesService>();
        preferences.LiveStreamPollingIntervalSeconds.Returns(1);

        await using var client = new ServiceBusClient(TestConnectionString);
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetClient().Returns(client);

        await using var sut = new LiveStreamService(preferences);

        // Act
        var startTask = sut.StartQueueStreamAsync(operations, "queue-a", peekOnly: true);
        var completedTask = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        completedTask.Should().Be(startTask, "peek-mode startup should not wait for the streaming loop to finish");
        await startTask;
        sut.IsStreaming.Should().BeTrue();

        await sut.StopStreamAsync();
    }
}
