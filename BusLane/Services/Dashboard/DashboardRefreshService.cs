using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.ServiceBus;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BusLane.Services.Dashboard;

public class DashboardRefreshService : IDashboardRefreshService
{
    private const int MaxConcurrentSubscriptionRefreshes = 4;

    public DateTimeOffset? LastRefreshTime { get; private set; }
    public bool IsRefreshing { get; private set; }

    public event EventHandler<NamespaceDashboardSummary>? SummaryUpdated;
    public event EventHandler<IReadOnlyList<TopEntityInfo>>? TopEntitiesUpdated;
    public event EventHandler<NamespaceEntitySnapshot>? EntitiesUpdated;

    private Timer? _refreshTimer;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private string? _currentNamespaceId;
    private IServiceBusOperations? _currentOperations;
    private CancellationTokenSource? _refreshCts;
    private long _refreshGeneration;
    private NamespaceDashboardSummary? _lastSummary;
    private int _autoRefreshTickInProgress;

    public async Task RefreshAsync(string namespaceId, IServiceBusOperations? operations = null, CancellationToken ct = default)
    {
        var context = GetOrCreateRefreshContext(namespaceId, operations);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, context.CancellationToken);
        try
        {
            await RefreshCoreAsync(context, linkedCts.Token);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Context changed or refresh stopped. Stale work must end silently.
        }
    }

    private async Task RefreshCoreAsync(RefreshContext context, CancellationToken ct)
    {
        await _refreshGate.WaitAsync(ct);
        IsRefreshing = true;

        try
        {
            var operations = context.Operations;

            if (operations == null)
            {
                // No operations available, return empty summary
                var emptySummary = new NamespaceDashboardSummary(
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

                if (IsCurrent(context.Generation))
                {
                    SummaryUpdated?.Invoke(this, emptySummary);
                    TopEntitiesUpdated?.Invoke(this, new List<TopEntityInfo>());
                    LastRefreshTime = DateTimeOffset.UtcNow;
                }
                return;
            }

            // Fetch all queues and topics
            var queuesTask = operations.GetQueuesAsync(ct);
            var topicsTask = operations.GetTopicsAsync(ct);

            await Task.WhenAll(queuesTask, topicsTask);

            var queues = queuesTask.Result.ToList();
            var topics = topicsTask.Result.ToList();

            // Fetch subscriptions for all topics to get complete message counts
            using var subscriptionGate = new SemaphoreSlim(MaxConcurrentSubscriptionRefreshes);
            var subscriptionTasks = FetchSubscriptionsForTopicsAsync(operations, topics, subscriptionGate, ct);
            var subscriptionResults = await Task.WhenAll(subscriptionTasks);
            var allSubscriptions = subscriptionResults.SelectMany(static subscriptions => subscriptions).ToList();

            // Calculate totals from queues
            long totalActiveMessages = queues.Sum(q => q.ActiveMessageCount);
            long totalDeadLetterMessages = queues.Sum(q => q.DeadLetterCount);
            long totalScheduledMessages = queues.Sum(q => q.ScheduledCount);
            long totalSizeInBytes = queues.Sum(q => q.SizeInBytes);

            // Add subscription totals
            totalActiveMessages += allSubscriptions.Sum(s => s.ActiveMessageCount);
            totalDeadLetterMessages += allSubscriptions.Sum(s => s.DeadLetterCount);
            totalSizeInBytes += topics.Sum(t => t.SizeInBytes);

            // Calculate growth percentages (comparing to last summary if available)
            double activeGrowth = CalculateGrowthPercentage(
                _lastSummary?.TotalActiveMessages ?? 0, totalActiveMessages);
            double deadLetterGrowth = CalculateGrowthPercentage(
                _lastSummary?.TotalDeadLetterMessages ?? 0, totalDeadLetterMessages);
            double scheduledGrowth = CalculateGrowthPercentage(
                _lastSummary?.TotalScheduledMessages ?? 0, totalScheduledMessages);
            double sizeGrowth = CalculateGrowthPercentage(
                _lastSummary?.TotalSizeInBytes ?? 0, totalSizeInBytes);

            var summary = new NamespaceDashboardSummary(
                TotalActiveMessages: totalActiveMessages,
                TotalDeadLetterMessages: totalDeadLetterMessages,
                TotalScheduledMessages: totalScheduledMessages,
                TotalSizeInBytes: totalSizeInBytes,
                ActiveMessagesGrowthPercentage: activeGrowth,
                DeadLetterGrowthPercentage: deadLetterGrowth,
                ScheduledGrowthPercentage: scheduledGrowth,
                SizeGrowthPercentage: sizeGrowth,
                Timestamp: DateTimeOffset.UtcNow
            );

            if (!IsCurrent(context.Generation))
            {
                return;
            }

            _lastSummary = summary;

            // Build top entities list
            var topEntities = BuildTopEntitiesList(queues, topics, allSubscriptions);
            var entitySnapshot = new NamespaceEntitySnapshot(
                queues,
                allSubscriptions,
                DateTimeOffset.UtcNow);

            SummaryUpdated?.Invoke(this, summary);
            TopEntitiesUpdated?.Invoke(this, topEntities);
            EntitiesUpdated?.Invoke(this, entitySnapshot);
            LastRefreshTime = DateTimeOffset.UtcNow;
        }
        finally
        {
            IsRefreshing = false;
            _refreshGate.Release();
        }
    }

    private static double CalculateGrowthPercentage(long previous, long current)
    {
        if (previous == 0)
        {
            return current > 0 ? 100.0 : 0.0;
        }

        return ((double)(current - previous) / previous) * 100.0;
    }

    private IEnumerable<Task<List<SubscriptionInfo>>> FetchSubscriptionsForTopicsAsync(
        IServiceBusOperations operations,
        IReadOnlyList<TopicInfo> topics,
        SemaphoreSlim gate,
        CancellationToken ct)
    {
        return topics.Select(topic => FetchSubscriptionsForTopicAsync(operations, topic.Name, gate, ct)).ToList();
    }

    private async Task<List<SubscriptionInfo>> FetchSubscriptionsForTopicAsync(
        IServiceBusOperations operations,
        string topicName,
        SemaphoreSlim gate,
        CancellationToken ct)
    {
        var gateEntered = false;
        try
        {
            await gate.WaitAsync(ct);
            gateEntered = true;
            return (await operations.GetSubscriptionsAsync(topicName, ct)).ToList();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Warning(ex, "Failed to fetch subscriptions for topic {TopicName}", topicName);
            return [];
        }
        finally
        {
            if (gateEntered)
            {
                gate.Release();
            }
        }
    }

    private static IReadOnlyList<TopEntityInfo> BuildTopEntitiesList(
        List<QueueInfo> queues,
        List<TopicInfo> topics,
        List<SubscriptionInfo> subscriptions)
    {
        var entities = new List<TopEntityInfo>();
        var queueCounts = queues
            .Select(q => new
            {
                q.Name,
                MessageCount = GetQueueMessageCount(q)
            })
            .ToList();

        var subscriptionCountsByTopic = subscriptions
            .GroupBy(s => s.TopicName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(GetSubscriptionMessageCount),
                StringComparer.OrdinalIgnoreCase);

        var topicCounts = topics
            .Select(t => new
            {
                t.Name,
                MessageCount = subscriptionCountsByTopic.GetValueOrDefault(t.Name, 0L)
            })
            .ToList();

        long totalQueueMessages = queueCounts.Sum(q => q.MessageCount);
        long totalTopicMessages = topicCounts.Sum(t => t.MessageCount);

        // Add queues
        foreach (var queue in queueCounts)
        {
            double percentage = totalQueueMessages > 0
                ? ((double)queue.MessageCount / totalQueueMessages) * 100.0
                : 0.0;

            entities.Add(new TopEntityInfo(
                Name: queue.Name,
                MessageCount: queue.MessageCount,
                PercentageOfTotal: percentage,
                Type: EntityType.Queue
            ));
        }

        // Add topics based on aggregated subscription message counts
        foreach (var topic in topicCounts)
        {
            double percentage = totalTopicMessages > 0
                ? ((double)topic.MessageCount / totalTopicMessages) * 100.0
                : 0.0;

            entities.Add(new TopEntityInfo(
                Name: topic.Name,
                MessageCount: topic.MessageCount,
                PercentageOfTotal: percentage,
                Type: EntityType.Topic
            ));
        }

        // Keep both lists in this collection; each UI list filters by entity type.
        return entities
            .OrderByDescending(e => e.MessageCount)
            .Take(20)
            .ToList();
    }

    private static long GetQueueMessageCount(QueueInfo queue)
    {
        if (queue.MessageCount > 0)
        {
            return queue.MessageCount;
        }

        // Fallback for providers that don't populate MessageCount consistently.
        return Math.Max(0, queue.ActiveMessageCount + queue.DeadLetterCount + queue.ScheduledCount);
    }

    private static long GetSubscriptionMessageCount(SubscriptionInfo subscription)
    {
        if (subscription.MessageCount > 0)
        {
            return subscription.MessageCount;
        }

        return Math.Max(0, subscription.ActiveMessageCount + subscription.DeadLetterCount);
    }

    public void StartAutoRefresh(string namespaceId, IServiceBusOperations? operations = null, TimeSpan? interval = null)
    {
        var context = GetOrCreateRefreshContext(namespaceId, operations);

        var actualInterval = interval ?? TimeSpan.FromSeconds(30);

        _refreshTimer?.Dispose();
        _refreshTimer = new Timer(
            _ => _ = OnAutoRefreshTickAsync(context),
            null,
            actualInterval,
            actualInterval
        );
    }

    private async Task OnAutoRefreshTickAsync(RefreshContext context)
    {
        if (Interlocked.CompareExchange(ref _autoRefreshTickInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await RefreshCoreAsync(context, context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Refresh was stopped or replaced by another namespace generation.
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Dashboard auto-refresh tick failed");
        }
        finally
        {
            Interlocked.Exchange(ref _autoRefreshTickInProgress, 0);
        }
    }

    public void StopAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        lock (_stateLock)
        {
            _refreshGeneration++;
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = null;
            _currentNamespaceId = null;
            _currentOperations = null;
            _lastSummary = null;
        }
    }

    private RefreshContext GetOrCreateRefreshContext(string namespaceId, IServiceBusOperations? operations)
    {
        lock (_stateLock)
        {
            var effectiveOperations = operations ?? _currentOperations;
            var contextChanged = !string.Equals(_currentNamespaceId, namespaceId, StringComparison.Ordinal) ||
                                 !ReferenceEquals(_currentOperations, effectiveOperations) ||
                                 _refreshCts == null;

            if (contextChanged)
            {
                _refreshGeneration++;
                _refreshCts?.Cancel();
                _refreshCts?.Dispose();
                _refreshCts = new CancellationTokenSource();
                _currentNamespaceId = namespaceId;
                _currentOperations = effectiveOperations;
                _lastSummary = null;
            }

            var refreshCts = _refreshCts ?? throw new InvalidOperationException("Refresh context was not initialized");
            return new RefreshContext(
                _refreshGeneration,
                _currentNamespaceId!,
                _currentOperations,
                refreshCts.Token);
        }
    }

    private bool IsCurrent(long generation)
    {
        lock (_stateLock)
        {
            return generation == _refreshGeneration && _refreshCts is { IsCancellationRequested: false };
        }
    }

    private sealed record RefreshContext(
        long Generation,
        string NamespaceId,
        IServiceBusOperations? Operations,
        CancellationToken CancellationToken);
}
