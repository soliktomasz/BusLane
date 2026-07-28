namespace BusLane.Services.ServiceBus;

public interface ICorrelationRefreshDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct = default);
}

public sealed class CorrelationRefreshDelay : ICorrelationRefreshDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken ct = default)
    {
        return Task.Delay(duration, ct);
    }
}
