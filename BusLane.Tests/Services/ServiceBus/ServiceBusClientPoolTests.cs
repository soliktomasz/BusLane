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
        await using var _sut = new ServiceBusClientPool();
        var first = _sut.GetClient(ConnectionString);

        // Act
        await _sut.ReturnClientAsync(ConnectionString, first);
        var second = _sut.GetClient(ConnectionString);

        // Assert
        second.Should().BeSameAs(first);
        _sut.GetStatistics().Should().Be(new ServiceBusClientPool.PoolStatistics(1, 1));
    }

    [Fact]
    public async Task GetAndReturnClientAsync_WhenConcurrent_AlwaysUsesSingleLiveClient()
    {
        // Arrange
        await using var _sut = new ServiceBusClientPool();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            await gate.Task;
            var client = _sut.GetClient(ConnectionString);
            await Task.Yield();
            await _sut.ReturnClientAsync(ConnectionString, client);
            return client;
        }).ToArray();
        gate.SetResult();
        var clients = await Task.WhenAll(tasks);

        // Assert
        clients.Should().OnlyContain(client => ReferenceEquals(client, clients[0]));
        _sut.GetStatistics().Should().Be(new ServiceBusClientPool.PoolStatistics(1, 0));
    }
}
