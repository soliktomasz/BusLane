namespace BusLane.Services.Monitoring;

using BusLane.Models;
using BusLane.Services.ServiceBus;

/// <summary>
/// Centralized wrapper for probing Service Bus connection health.
/// </summary>
public interface IHealthCheckService
{
    Task<ConnectionHealthReport> ProbeAsync(IServiceBusOperations operations, CancellationToken ct = default);
}
