using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
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
    private IServiceBusOperations? _currentOperations;
    private NamespaceDashboardSummary? _lastSummary;

    public async Task RefreshAsync(string namespaceId, IServiceBusOperations? operations = null, CancellationToken ct = default)
    {
        IsRefreshing = true;
        _currentNamespaceId = namespaceId;

        if (operations != null)
        {
            _currentOperations = operations;
        }

        try
        {
            if (_currentOperations == null)
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

                SummaryUpdated?.Invoke(this, emptySummary);
                TopEntitiesUpdated?.Invoke(this, new List<TopEntityInfo>());
                LastRefreshTime = DateTimeOffset.UtcNow;
                return;
            }

            // Fetch all queues and topics
            var queuesTask = _currentOperations.GetQueuesAsync(ct);
            var topicsTask = _currentOperations.GetTopicsAsync(ct);

            await Task.WhenAll(queuesTask, topicsTask);

            var queues = queuesTask.Result.ToList();
            var topics = topicsTask.Result.ToList();

            // Fetch subscriptions for all topics to get complete message counts
            var allSubscriptions = new List<SubscriptionInfo>();
            foreach (var topic in topics)
            {
                try
                {
                    var subs = await _currentOperations.GetSubscriptionsAsync(topic.Name, ct);
                    allSubscriptions.AddRange(subs);
                }
                catch (Exception)
                {
                    // Ignore errors for individual topics
                }
            }

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

            _lastSummary = summary;

            // Build top entities list
            var topEntities = BuildTopEntitiesList(queues, topics, allSubscriptions);

            SummaryUpdated?.Invoke(this, summary);
            TopEntitiesUpdated?.Invoke(this, topEntities);
            LastRefreshTime = DateTimeOffset.UtcNow;
        }
        finally
        {
            IsRefreshing = false;
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
        _currentNamespaceId = namespaceId;

        if (operations != null)
        {
            _currentOperations = operations;
        }

        var actualInterval = interval ?? TimeSpan.FromSeconds(30);

        _refreshTimer?.Dispose();
        _refreshTimer = new Timer(
            async _ => await RefreshAsync(namespaceId, _currentOperations),
            null,
            TimeSpan.Zero,
            actualInterval
        );
    }

    public void StopAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        _currentNamespaceId = null;
        _currentOperations = null;
        _lastSummary = null;
    }
}
