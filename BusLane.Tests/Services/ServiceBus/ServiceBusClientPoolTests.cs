namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Services.ServiceBus;
using FluentAssertions;

public class ServiceBusClientPoolTests
{
    private const string ConnectionString =
        "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test123";

    [Fact]
    public async Task ReturnClientAsync_WhenClientIsIdle_KeepsClientAvailableForReuse()
    {
        // Arrange
        await using var sut = new ServiceBusClientPool();
        var first = sut.GetClient(ConnectionString);

        // Act
        await sut.ReturnClientAsync(ConnectionString, first);
        var second = sut.GetClient(ConnectionString);

        // Assert
        second.Should().BeSameAs(first);
        sut.GetStatistics().Should().Be(new ServiceBusClientPool.PoolStatistics(1, 1));
    }

    [Fact]
    public async Task GetAndReturnClientAsync_WhenConcurrent_AlwaysUsesSingleLiveClient()
    {
        // Arrange
        await using var sut = new ServiceBusClientPool();

        // Act
        var clients = await Task.WhenAll(Enumerable.Range(0, 100).Select(async _ =>
        {
            var client = sut.GetClient(ConnectionString);
            await Task.Yield();
            await sut.ReturnClientAsync(ConnectionString, client);
            return client;
        }));

        // Assert
        clients.Should().OnlyContain(client => ReferenceEquals(client, clients[0]));
        sut.GetStatistics().Should().Be(new ServiceBusClientPool.PoolStatistics(1, 0));
    }
}
