using BusLane.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusLane.Services.Dashboard;

public class DashboardRefreshService : IDashboardRefreshService
{
    public DateTimeOffset? LastRefreshTime { get; private set; }
    public bool IsRefreshing { get; private set; }
    
    public event EventHandler<NamespaceDashboardSummary>? SummaryUpdated;
    public event EventHandler<IReadOnlyList<TopEntityInfo>>? TopEntitiesUpdated;
    
    private Timer? _refreshTimer;
    private string? _currentNamespaceId;

    public Task RefreshAsync(string namespaceId, CancellationToken ct = default)
    {
        IsRefreshing = true;
        
        try
        {
            // TODO: Fetch actual data from Service Bus
            var summary = new NamespaceDashboardSummary(
                TotalActiveMessages: 0,
                TotalDeadLetterMessages: 0,
                TotalScheduledMessages: 0,
                TotalSizeInBytes: 0,
                ActiveMessagesGrowthPercentage: 0,
                DeadLetterGrowthPercentage: 0,
                ScheduledGrowthPercentage: 0,
                SizeGrowthPercentage: 0,
                Timestamp: DateTimeOffset.UtcNow
            );
            
            SummaryUpdated?.Invoke(this, summary);
            TopEntitiesUpdated?.Invoke(this, new List<TopEntityInfo>());
            LastRefreshTime = DateTimeOffset.UtcNow;
        }
        finally
        {
            IsRefreshing = false;
        }
        
        return Task.CompletedTask;
    }

    public void StartAutoRefresh(string namespaceId, TimeSpan interval)
    {
        _currentNamespaceId = namespaceId;
        _refreshTimer?.Dispose();
        _refreshTimer = new Timer(
            async _ => await RefreshAsync(namespaceId),
            null,
            TimeSpan.Zero,
            interval
        );
    }

    public void StopAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        _currentNamespaceId = null;
    }
}
