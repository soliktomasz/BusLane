namespace BusLane.Services.Monitoring;

using BusLane.Models;
using BusLane.Services.ServiceBus;

public class HealthCheckService : IHealthCheckService
{
    public Task<ConnectionHealthReport> ProbeAsync(IServiceBusOperations operations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return operations.CheckConnectionHealthAsync(ct);
    }
}
