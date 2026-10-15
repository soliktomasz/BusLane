namespace BusLane.Services.ServiceBus;

using Azure;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.ResourceManager.ServiceBus;
using Azure.ResourceManager.ServiceBus.Models;
using BusLane.Models;
using Serilog;

/// <summary>
/// Service Bus operations using Azure credential (DefaultAzureCredential/InteractiveBrowserCredential) for authentication.
/// Caches ServiceBusClient for connection pooling.
/// </summary>
public class AzureCredentialOperations : ServiceBusOperationsBase, IAzureCredentialOperations
{
    private readonly string _endpoint;
    private readonly string _namespaceId;
    private readonly TokenCredential _credential;
    private readonly Func<ServiceBusNamespaceResource> _getNamespaceResource;
    private readonly Lazy<ServiceBusAdministrationClient> _adminClient;
    private readonly Lazy<ServiceBusClient> _client;
    private bool _disposed;

    public string NamespaceId => _namespaceId;

    public AzureCredentialOperations(
        string endpoint,
        string namespaceId,
        TokenCredential credential,
        Func<ServiceBusNamespaceResource> getNamespaceResource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(getNamespaceResource);

        _endpoint = NormalizeEndpoint(endpoint);
        _namespaceId = namespaceId;
        _credential = credential;
        _getNamespaceResource = getNamespaceResource;
        _adminClient = new Lazy<ServiceBusAdministrationClient>(() => new ServiceBusAdministrationClient(_endpoint, _credential));
        _client = new Lazy<ServiceBusClient>(() => new ServiceBusClient(_endpoint, _credential));
        Log.Debug("AzureCredentialOperations initialized for endpoint {Endpoint}", _endpoint);
    }

    public override ServiceBusClient GetClient() => _client.Value;
    protected override ServiceBusAdministrationClient AdminClient => _adminClient.Value;
    protected override string ReauthenticateHint => "Sign in again or refresh Azure credentials.";

    // Entity listing comes from ARM, so the health probe checks ARM too.
    protected override async Task ProbeAsync(CancellationToken ct)
    {
        await foreach (var _ in _getNamespaceResource().GetServiceBusQueues().GetAllAsync(cancellationToken: ct))
        {
            break;
        }
    }

    protected override string NormalizeEntityName(string entityName) => NormalizeEntityPath(entityName);

    public async Task<IEnumerable<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
        Log.Debug("Fetching queues from {Endpoint}", _endpoint);
        var ns = _getNamespaceResource();
        var queues = new List<QueueInfo>();

        await foreach (var q in ns.GetServiceBusQueues().GetAllAsync(cancellationToken: ct))
        {
            queues.Add(new QueueInfo(
                q.Data.Name,
                q.Data.MessageCount ?? 0,
                q.Data.CountDetails?.ActiveMessageCount ?? 0,
                q.Data.CountDetails?.DeadLetterMessageCount ?? 0,
                q.Data.CountDetails?.ScheduledMessageCount ?? 0,
                q.Data.SizeInBytes ?? 0,
                q.Data.AccessedOn,
                q.Data.RequiresSession ?? false,
                q.Data.DefaultMessageTimeToLive ?? ServiceBusOperations.Options.DefaultMessageTimeToLive,
                q.Data.LockDuration ?? ServiceBusOperations.Options.DefaultLockDuration
            ));
        }
        Log.Debug("Retrieved {Count} queues from {Endpoint}", queues.Count, _endpoint);
        return queues;
    }

    public override async Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken ct = default)
    {
        try
        {
            var ns = _getNamespaceResource();
            var queue = await ns.GetServiceBusQueueAsync(queueName, ct);
            var q = queue.Value;

            return new QueueInfo(
                q.Data.Name,
                q.Data.MessageCount ?? 0,
                q.Data.CountDetails?.ActiveMessageCount ?? 0,
                q.Data.CountDetails?.DeadLetterMessageCount ?? 0,
                q.Data.CountDetails?.ScheduledMessageCount ?? 0,
                q.Data.SizeInBytes ?? 0,
                q.Data.AccessedOn,
                q.Data.RequiresSession ?? false,
                q.Data.DefaultMessageTimeToLive ?? ServiceBusOperations.Options.DefaultMessageTimeToLive,
                q.Data.LockDuration ?? ServiceBusOperations.Options.DefaultLockDuration
            );
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to get queue info for {QueueName}", queueName);
            return null;
        }
    }

    public async Task<IEnumerable<TopicInfo>> GetTopicsAsync(CancellationToken ct = default)
    {
        var ns = _getNamespaceResource();
        var topics = new List<TopicInfo>();

        await foreach (var t in ns.GetServiceBusTopics().GetAllAsync(cancellationToken: ct))
        {
            topics.Add(new TopicInfo(
                t.Data.Name,
                t.Data.SizeInBytes ?? 0,
                t.Data.SubscriptionCount ?? 0,
                t.Data.AccessedOn,
                t.Data.DefaultMessageTimeToLive ?? ServiceBusOperations.Options.DefaultMessageTimeToLive
            ));
        }
        return topics;
    }

    public async Task<TopicInfo?> GetTopicInfoAsync(string topicName, CancellationToken ct = default)
    {
        try
        {
            var ns = _getNamespaceResource();
            var topic = await ns.GetServiceBusTopicAsync(topicName, ct);
            var t = topic.Value;

            return new TopicInfo(
                t.Data.Name,
                t.Data.SizeInBytes ?? 0,
                t.Data.SubscriptionCount ?? 0,
                t.Data.AccessedOn,
                t.Data.DefaultMessageTimeToLive ?? ServiceBusOperations.Options.DefaultMessageTimeToLive
            );
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to get topic info for {TopicName}", topicName);
            return null;
        }
    }

    public override async Task<IEnumerable<SubscriptionInfo>> GetSubscriptionsAsync(string topicName, CancellationToken ct = default)
    {
        var ns = _getNamespaceResource();
        var topic = await ns.GetServiceBusTopicAsync(topicName, ct);
        var subs = new List<SubscriptionInfo>();

        await foreach (var s in topic.Value.GetServiceBusSubscriptions().GetAllAsync(cancellationToken: ct))
        {
            subs.Add(new SubscriptionInfo(
                s.Data.Name,
                topicName,
                s.Data.MessageCount ?? 0,
                s.Data.CountDetails?.ActiveMessageCount ?? 0,
                s.Data.CountDetails?.DeadLetterMessageCount ?? 0,
                s.Data.AccessedOn,
                s.Data.RequiresSession ?? false,
                s.Data.LockDuration,
                s.Data.MaxDeliveryCount,
                s.Data.DefaultMessageTimeToLive,
                s.Data.AutoDeleteOnIdle,
                s.Data.ForwardTo,
                s.Data.ForwardDeadLetteredMessagesTo,
                s.Data.EnableBatchedOperations,
                s.Data.DeadLetteringOnMessageExpiration,
                s.Data.Status?.ToString(),
                s.Data.CreatedOn,
                s.Data.UpdatedOn,
                s.Data.CountDetails?.TransferMessageCount ?? 0,
                s.Data.CountDetails?.TransferDeadLetterMessageCount ?? 0
            ));
        }
        return subs;
    }

    public async Task CreateSubscriptionAsync(
        string topicName,
        SubscriptionCreationOptions options,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new ArgumentException("Subscription name is required.", nameof(SubscriptionCreationOptions.Name));
        }

        var ns = _getNamespaceResource();
        _ = await ns.GetServiceBusTopicAsync(topicName, ct);
        var sdkOptions = ServiceBusOperations.BuildCreateSubscriptionOptions(topicName, options);
        await AdminClient.CreateSubscriptionAsync(sdkOptions, ct);
    }

    public async Task DeleteSubscriptionAsync(
        string topicName,
        string subscriptionName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        var ns = _getNamespaceResource();
        var topic = await ns.GetServiceBusTopicAsync(topicName, ct);
        var subscription = await topic.Value.GetServiceBusSubscriptionAsync(subscriptionName, ct);
        await subscription.Value.DeleteAsync(WaitUntil.Completed, ct);
    }

    public async Task DeleteQueueAsync(string queueName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var ns = _getNamespaceResource();
        var queue = await ns.GetServiceBusQueueAsync(queueName, ct);
        await queue.Value.DeleteAsync(WaitUntil.Completed, ct);
    }

    public async Task DeleteTopicAsync(string topicName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

        var ns = _getNamespaceResource();
        var topic = await ns.GetServiceBusTopicAsync(topicName, ct);
        await topic.Value.DeleteAsync(WaitUntil.Completed, ct);
    }

    public async Task UpdateQueueAsync(string queueName, QueueUpdateOptions options, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(options);

        var ns = _getNamespaceResource();
        var queue = (await ns.GetServiceBusQueueAsync(queueName, ct)).Value;
        var data = queue.Data;
        if (options.LockDuration.HasValue)
        {
            data.LockDuration = options.LockDuration.Value;
        }

        if (options.DefaultMessageTimeToLive.HasValue)
        {
            data.DefaultMessageTimeToLive = options.DefaultMessageTimeToLive.Value;
        }

        if (options.DeadLetteringOnMessageExpiration.HasValue)
        {
            data.DeadLetteringOnMessageExpiration = options.DeadLetteringOnMessageExpiration.Value;
        }

        if (options.EnableBatchedOperations.HasValue)
        {
            data.EnableBatchedOperations = options.EnableBatchedOperations.Value;
        }

        await queue.UpdateAsync(WaitUntil.Completed, data, ct);
    }

    public async Task UpdateTopicAsync(string topicName, TopicUpdateOptions options, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentNullException.ThrowIfNull(options);

        var ns = _getNamespaceResource();
        var topic = (await ns.GetServiceBusTopicAsync(topicName, ct)).Value;
        var data = topic.Data;
        if (options.DefaultMessageTimeToLive.HasValue)
        {
            data.DefaultMessageTimeToLive = options.DefaultMessageTimeToLive.Value;
        }

        if (options.EnableBatchedOperations.HasValue)
        {
            data.EnableBatchedOperations = options.EnableBatchedOperations.Value;
        }

        await topic.UpdateAsync(WaitUntil.Completed, data, ct);
    }

    public async Task UpdateSubscriptionAsync(
        string topicName,
        string subscriptionName,
        SubscriptionUpdateOptions options,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        ArgumentNullException.ThrowIfNull(options);

        var ns = _getNamespaceResource();
        var topic = (await ns.GetServiceBusTopicAsync(topicName, ct)).Value;
        var subscription = (await topic.GetServiceBusSubscriptionAsync(subscriptionName, ct)).Value;
        var data = subscription.Data;
        if (options.LockDuration.HasValue)
        {
            data.LockDuration = options.LockDuration.Value;
        }

        if (options.MaxDeliveryCount.HasValue)
        {
            data.MaxDeliveryCount = options.MaxDeliveryCount.Value;
        }

        if (options.DefaultMessageTimeToLive.HasValue)
        {
            data.DefaultMessageTimeToLive = options.DefaultMessageTimeToLive.Value;
        }

        if (options.AutoDeleteOnIdle.HasValue)
        {
            data.AutoDeleteOnIdle = options.AutoDeleteOnIdle.Value;
        }

        if (options.ForwardTo != null)
        {
            data.ForwardTo = string.IsNullOrWhiteSpace(options.ForwardTo) ? null : options.ForwardTo;
        }

        if (options.ForwardDeadLetteredMessagesTo != null)
        {
            data.ForwardDeadLetteredMessagesTo = string.IsNullOrWhiteSpace(options.ForwardDeadLetteredMessagesTo)
                ? null
                : options.ForwardDeadLetteredMessagesTo;
        }

        if (options.EnableBatchedOperations.HasValue)
        {
            data.EnableBatchedOperations = options.EnableBatchedOperations.Value;
        }

        if (options.DeadLetteringOnMessageExpiration.HasValue)
        {
            data.DeadLetteringOnMessageExpiration = options.DeadLetteringOnMessageExpiration.Value;
        }

        await subscription.UpdateAsync(WaitUntil.Completed, data, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_client.IsValueCreated)
        {
            await _client.Value.DisposeAsync();
        }
    }

    #region Private Helpers

    private static string NormalizeEndpoint(string endpoint)
    {
        var fqn = endpoint
            .Replace("sb://", "")
            .Replace("https://", "")
            .TrimEnd('/');

        if (!fqn.Contains('.'))
            fqn = $"{fqn}.servicebus.windows.net";

        return fqn;
    }

    internal static string NormalizeEntityPath(string entityPath) =>
        entityPath.Contains('~') ? entityPath.Replace('~', '/') : entityPath;

    #endregion
}
