using BusLane.Models.Dashboard;
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
    
    Task RefreshAsync(string namespaceId, CancellationToken ct = default);
    void StartAutoRefresh(string namespaceId, TimeSpan interval);
    void StopAutoRefresh();
}
