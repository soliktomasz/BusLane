namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Services.ServiceBus;
using FluentAssertions;

public class CorrelationRefreshDelayTests
{
    [Fact]
    public async Task DelayAsync_WithZeroDuration_Completes()
    {
        var sut = new CorrelationRefreshDelay();

        var action = async () => await sut.DelayAsync(TimeSpan.Zero);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DelayAsync_WithCancelledToken_PropagatesCancellation()
    {
        var sut = new CorrelationRefreshDelay();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var action = async () => await sut.DelayAsync(TimeSpan.FromSeconds(1), cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
