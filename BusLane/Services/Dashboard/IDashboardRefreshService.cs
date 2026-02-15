using BusLane.Models.Dashboard;
using BusLane.Services.ServiceBus;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusLane.Services.Dashboard;

public interface IDashboardRefreshService
{
    DateTimeOffset? LastRefreshTime { get; }
    bool IsRefreshing { get; }

    event EventHandler<NamespaceDashboardSummary>? SummaryUpdated;
    event EventHandler<IReadOnlyList<TopEntityInfo>>? TopEntitiesUpdated;

    Task RefreshAsync(string namespaceId, IServiceBusOperations? operations = null, CancellationToken ct = default);
    void StartAutoRefresh(string namespaceId, IServiceBusOperations? operations = null, TimeSpan? interval = null);
    void StopAutoRefresh();
}
