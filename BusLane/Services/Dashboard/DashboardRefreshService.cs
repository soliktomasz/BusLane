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
    private const int MaxTopicsPerRefresh = 4;

    public DateTimeOffset? LastRefreshTime { get; private set; }
    public bool IsRefreshing { get; private set; }

    public event EventHandler<NamespaceDashboardSummary>? SummaryUpdated;
    public event EventHandler<IReadOnlyList<TopEntityInfo>>? TopEntitiesUpdated;
    public event EventHandler<NamespaceEntitySnapshot>? EntitiesUpdated;
    public event EventHandler<DashboardRefreshFailure>? RefreshFailed;

    private Timer? _refreshTimer;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private string? _currentNamespaceId;
    private IServiceBusOperations? _currentOperations;
    private CancellationTokenSource? _refreshCts;
    private long _refreshGeneration;
    private DashboardRefreshCache? _refreshCache;
    private NamespaceDashboardSummary? _lastSummary;
    private int _autoRefreshTickInProgress;

    public async Task RefreshAsync(string namespaceId, IServiceBusOperations? operations = null, CancellationToken ct = default)
    {
        var context = GetOrCreateRefreshContext(namespaceId, operations);
        await ExecuteRefreshAsync(context, section: null, ct);
    }

    public async Task RefreshSectionAsync(
        string namespaceId,
        DashboardRefreshSection section,
        IServiceBusOperations? operations = null,
        CancellationToken ct = default)
    {
        var context = GetOrCreateRefreshContext(namespaceId, operations);
        await ExecuteRefreshAsync(context, section, ct);
    }

    private async Task ExecuteRefreshAsync(
        RefreshContext context,
        DashboardRefreshSection? section,
        CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, context.CancellationToken);
        try
        {
            await RefreshCoreAsync(context, section, linkedCts.Token);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Context changed or refresh stopped. Stale work must end silently.
        }
    }

    private async Task RefreshCoreAsync(
        RefreshContext context,
        DashboardRefreshSection? requestedSection,
        CancellationToken ct)
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

            var refreshQueues = requestedSection is null or DashboardRefreshSection.Queues;
            var refreshTopics = requestedSection is null or DashboardRefreshSection.Topics or DashboardRefreshSection.Subscriptions;
            var queuesTask = refreshQueues ? FetchQueuesAsync(operations, ct) : null;
            var topicsTask = refreshTopics ? FetchTopicsAsync(operations, ct) : null;
            if (queuesTask is not null && topicsTask is not null)
            {
                await Task.WhenAll(queuesTask, topicsTask);
            }
            else if (queuesTask is not null)
            {
                await queuesTask;
            }
            else if (topicsTask is not null)
            {
                await topicsTask;
            }

            var refreshedSections = new List<DashboardRefreshSection>();
            var rootFailure = false;
            if (queuesTask is not null)
            {
                var result = await queuesTask;
                if (result.Succeeded)
                {
                    context.Cache.Queues = result.Items;
                    context.Cache.HasQueues = true;
                    refreshedSections.Add(DashboardRefreshSection.Queues);
                }
                else
                {
                    rootFailure = true;
                    PublishFailure(context, DashboardRefreshSection.Queues, "Queues could not be refreshed.");
                }
            }

            var subscriptionsPartial = context.Cache.Topics.Any(topic =>
                !context.Cache.SubscriptionsByTopic.ContainsKey(topic.Name));
            if (topicsTask is not null)
            {
                var result = await topicsTask;
                if (result.Succeeded)
                {
                    context.Cache.Topics = result.Items;
                    context.Cache.HasTopics = true;
                    refreshedSections.Add(DashboardRefreshSection.Topics);
                    var subscriptionResult = await RefreshSubscriptionsAsync(
                        operations,
                        context.Cache.Topics,
                        context.Cache,
                        ct);
                    subscriptionsPartial = subscriptionResult.IsPartial;
                    if (subscriptionResult.HadFailures)
                    {
                        PublishFailure(context, DashboardRefreshSection.Subscriptions, "Some subscriptions could not be refreshed.");
                    }
                    else
                    {
                        refreshedSections.Add(DashboardRefreshSection.Subscriptions);
                    }
                }
                else
                {
                    rootFailure = true;
                    subscriptionsPartial = true;
                    PublishFailure(context, DashboardRefreshSection.Topics, "Topics could not be refreshed.");
                }
            }

            if (!IsCurrent(context.Generation) || refreshedSections.Count == 0)
            {
                return;
            }

            var queues = context.Cache.Queues;
            var topics = context.Cache.Topics;
            var allSubscriptions = context.Cache.Topics
                .Where(topic => context.Cache.SubscriptionsByTopic.ContainsKey(topic.Name))
                .SelectMany(topic => context.Cache.SubscriptionsByTopic[topic.Name])
                .ToList();
            var isPartial = rootFailure
                || subscriptionsPartial
                || !context.Cache.HasQueues
                || !context.Cache.HasTopics;

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
                Timestamp: DateTimeOffset.UtcNow,
                IsPartial: isPartial
            );

            if (!IsCurrent(context.Generation))
            {
                return;
            }

            if (!isPartial)
            {
                _lastSummary = summary;
            }

            // Build top entities list
            var topEntities = BuildTopEntitiesList(queues, topics, allSubscriptions);
            var entitySnapshot = new NamespaceEntitySnapshot(
                queues,
                allSubscriptions,
                DateTimeOffset.UtcNow,
                refreshedSections);

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

    private async Task<(List<SubscriptionInfo> Subscriptions, bool IsPartial, bool HadFailures)> RefreshSubscriptionsAsync(
        IServiceBusOperations operations,
        IReadOnlyList<TopicInfo> topics,
        DashboardRefreshCache cache,
        CancellationToken ct)
    {
        var activeTopicNames = topics.Select(topic => topic.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removedTopicName in cache.SubscriptionsByTopic.Keys
                     .Where(name => !activeTopicNames.Contains(name))
                     .ToList())
        {
            cache.SubscriptionsByTopic.Remove(removedTopicName);
        }

        var topicsToRefresh = SelectTopicsToRefresh(topics, cache);
        using var gate = new SemaphoreSlim(MaxConcurrentSubscriptionRefreshes);
        var results = await Task.WhenAll(topicsToRefresh.Select(async topic => new
        {
            topic.Name,
            Result = await FetchSubscriptionsForTopicAsync(operations, topic.Name, gate, ct)
        }));

        foreach (var result in results.Where(result => result.Result.Succeeded))
        {
            cache.SubscriptionsByTopic[result.Name] = result.Result.Subscriptions;
        }

        var subscriptions = topics
            .Where(topic => cache.SubscriptionsByTopic.ContainsKey(topic.Name))
            .SelectMany(topic => cache.SubscriptionsByTopic[topic.Name])
            .ToList();
        return (
            subscriptions,
            topics.Any(topic => !cache.SubscriptionsByTopic.ContainsKey(topic.Name)),
            results.Any(result => !result.Result.Succeeded));
    }

    private static async Task<SectionFetchResult<QueueInfo>> FetchQueuesAsync(
        IServiceBusOperations operations,
        CancellationToken ct)
    {
        try
        {
            return new SectionFetchResult<QueueInfo>(true, (await operations.GetQueuesAsync(ct)).ToList());
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Warning(ex, "Failed to fetch queues for dashboard");
            return new SectionFetchResult<QueueInfo>(false, []);
        }
    }

    private static async Task<SectionFetchResult<TopicInfo>> FetchTopicsAsync(
        IServiceBusOperations operations,
        CancellationToken ct)
    {
        try
        {
            return new SectionFetchResult<TopicInfo>(true, (await operations.GetTopicsAsync(ct)).ToList());
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Warning(ex, "Failed to fetch topics for dashboard");
            return new SectionFetchResult<TopicInfo>(false, []);
        }
    }

    private void PublishFailure(
        RefreshContext context,
        DashboardRefreshSection section,
        string message)
    {
        if (IsCurrent(context.Generation))
        {
            RefreshFailed?.Invoke(this, new DashboardRefreshFailure(section, message, DateTimeOffset.UtcNow));
        }
    }

    private static List<TopicInfo> SelectTopicsToRefresh(
        IReadOnlyList<TopicInfo> availableTopics,
        DashboardRefreshCache cache)
    {
        var selected = availableTopics
            .Where(topic => !cache.SubscriptionsByTopic.ContainsKey(topic.Name))
            .Take(MaxTopicsPerRefresh)
            .ToList();
        if (selected.Count == MaxTopicsPerRefresh || availableTopics.Count == 0)
        {
            return selected;
        }

        var selectedNames = selected.Select(topic => topic.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remainingBudget = MaxTopicsPerRefresh - selected.Count;
        for (var offset = 0; offset < availableTopics.Count && remainingBudget > 0; offset++)
        {
            var index = (cache.NextTopicIndex + offset) % availableTopics.Count;
            var topic = availableTopics[index];
            if (selectedNames.Add(topic.Name))
            {
                selected.Add(topic);
                remainingBudget--;
            }
        }

        cache.NextTopicIndex = (cache.NextTopicIndex + MaxTopicsPerRefresh) % availableTopics.Count;
        return selected;
    }

    private async Task<(bool Succeeded, List<SubscriptionInfo> Subscriptions)> FetchSubscriptionsForTopicAsync(
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
            return (true, (await operations.GetSubscriptionsAsync(topicName, ct)).ToList());
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Warning(ex, "Failed to fetch subscriptions for topic {TopicName}", topicName);
            return (false, []);
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

        // Rank each entity type independently so a busy queue set cannot starve topics.
        return entities
            .Where(entity => entity.Type == EntityType.Queue)
            .OrderByDescending(entity => entity.MessageCount)
            .Take(20)
            .Concat(entities
                .Where(entity => entity.Type == EntityType.Topic)
                .OrderByDescending(entity => entity.MessageCount)
                .Take(20))
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
            await RefreshCoreAsync(context, requestedSection: null, context.CancellationToken);
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
            _refreshCache = null;
            _currentNamespaceId = null;
            _currentOperations = null;
            _lastSummary = null;
        }
    }

    private RefreshContext GetOrCreateRefreshContext(string namespaceId, IServiceBusOperations? operations)
    {
        lock (_stateLock)
        {
            var effectiveOperations = operations ??
                                      (string.Equals(_currentNamespaceId, namespaceId, StringComparison.Ordinal)
                                          ? _currentOperations
                                          : null);
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
                _refreshCache = new DashboardRefreshCache();
                _lastSummary = null;
            }

            var refreshCts = _refreshCts ?? throw new InvalidOperationException("Refresh context was not initialized");
            var refreshCache = _refreshCache ?? throw new InvalidOperationException("Refresh cache was not initialized");
            return new RefreshContext(
                _refreshGeneration,
                _currentNamespaceId!,
                _currentOperations,
                refreshCache,
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
        DashboardRefreshCache Cache,
        CancellationToken CancellationToken);

    private sealed record SectionFetchResult<T>(bool Succeeded, List<T> Items);

    private sealed class DashboardRefreshCache
    {
        public List<QueueInfo> Queues { get; set; } = [];
        public List<TopicInfo> Topics { get; set; } = [];
        public bool HasQueues { get; set; }
        public bool HasTopics { get; set; }
        public Dictionary<string, List<SubscriptionInfo>> SubscriptionsByTopic { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public int NextTopicIndex { get; set; }
    }
}
