namespace BusLane.Services.ServiceBus;

using System.Text.Json;
using BusLane.Models;

public interface INamespaceTopologyService
{
    Task<NamespaceTopologyDocument> ExportAsync(IServiceBusOperations operations, CancellationToken ct = default);
    Task<TopologyImportPlan> BuildImportPlanAsync(IServiceBusOperations operations, NamespaceTopologyDocument document, CancellationToken ct = default);
    Task ApplyImportPlanAsync(IServiceBusOperations operations, NamespaceTopologyDocument document, TopologyImportPlan plan, CancellationToken ct = default);
}

public static class NamespaceTopologySerializer
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string Serialize(NamespaceTopologyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static NamespaceTopologyDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<NamespaceTopologyDocument>(json, Options)
            ?? throw new InvalidOperationException("Namespace topology document is empty.");
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported namespace topology schema version {document.SchemaVersion}. Current version is {CurrentSchemaVersion}.");
        }

        return document;
    }
}

public class NamespaceTopologyService : INamespaceTopologyService
{
    private readonly Func<DateTimeOffset> _nowProvider;

    public NamespaceTopologyService(Func<DateTimeOffset>? nowProvider = null)
    {
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<NamespaceTopologyDocument> ExportAsync(IServiceBusOperations operations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var queues = (await operations.GetQueuesAsync(ct))
            .OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
            .Select(q => new QueueTopology(
                q.Name,
                q.RequiresSession,
                q.DefaultMessageTtl,
                q.LockDuration,
                DeadLetteringOnMessageExpiration: null,
                EnableBatchedOperations: null))
            .ToList();

        var topics = new List<TopicTopology>();
        foreach (var topic in (await operations.GetTopicsAsync(ct)).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var subscriptions = new List<SubscriptionTopology>();
            foreach (var subscription in (await operations.GetSubscriptionsAsync(topic.Name, ct)).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                var rules = (await operations.GetSubscriptionRulesAsync(topic.Name, subscription.Name, ct))
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(r => new RuleTopology(
                        r.Name,
                        r.FilterType,
                        r.DisplayExpression,
                        r.SqlExpression,
                        r.ActionExpression,
                        r.CorrelationProperties))
                    .ToList();

                subscriptions.Add(new SubscriptionTopology(
                    subscription.Name,
                    subscription.RequiresSession,
                    subscription.LockDuration,
                    subscription.MaxDeliveryCount,
                    subscription.DefaultMessageTimeToLive,
                    subscription.AutoDeleteOnIdle,
                    subscription.ForwardTo,
                    subscription.ForwardDeadLetteredMessagesTo,
                    subscription.EnableBatchedOperations,
                    subscription.DeadLetteringOnMessageExpiration,
                    rules));
            }

            topics.Add(new TopicTopology(
                topic.Name,
                topic.DefaultMessageTtl,
                EnableBatchedOperations: null,
                subscriptions));
        }

        return new NamespaceTopologyDocument(
            NamespaceTopologySerializer.CurrentSchemaVersion,
            _nowProvider(),
            queues,
            topics);
    }

    public async Task<TopologyImportPlan> BuildImportPlanAsync(
        IServiceBusOperations operations,
        NamespaceTopologyDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(document);

        var actions = new List<TopologyImportAction>();
        var existingQueues = (await operations.GetQueuesAsync(ct)).ToDictionary(q => q.Name, StringComparer.OrdinalIgnoreCase);
        var existingTopics = (await operations.GetTopicsAsync(ct)).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var queue in document.Queues.OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!existingQueues.TryGetValue(queue.Name, out var existingQueue))
            {
                actions.Add(new TopologyImportAction(TopologyImportActionType.CreateQueue, queue.Name, $"Create queue {queue.Name}"));
            }
            else if (QueueNeedsUpdate(existingQueue, queue))
            {
                actions.Add(new TopologyImportAction(TopologyImportActionType.UpdateQueue, queue.Name, $"Update queue {queue.Name}"));
            }
            else
            {
                actions.Add(new TopologyImportAction(TopologyImportActionType.Skip, queue.Name, $"Queue {queue.Name} already matches"));
            }
        }

        foreach (var topic in document.Topics.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var topicExists = existingTopics.TryGetValue(topic.Name, out var existingTopic);
            if (!topicExists)
            {
                actions.Add(new TopologyImportAction(TopologyImportActionType.CreateTopic, topic.Name, $"Create topic {topic.Name}"));
            }
            else if (existingTopic!.DefaultMessageTtl != topic.DefaultMessageTimeToLive)
            {
                actions.Add(new TopologyImportAction(TopologyImportActionType.UpdateTopic, topic.Name, $"Update topic {topic.Name}"));
            }
            else
            {
                actions.Add(new TopologyImportAction(TopologyImportActionType.Skip, topic.Name, $"Topic {topic.Name} already matches"));
            }

            var existingSubscriptions = topicExists
                ? (await operations.GetSubscriptionsAsync(topic.Name, ct)).ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, SubscriptionInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var subscription in topic.Subscriptions.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                var subscriptionPath = $"{topic.Name}/{subscription.Name}";
                var subscriptionExists = existingSubscriptions.TryGetValue(subscription.Name, out var existingSubscription);
                if (!subscriptionExists)
                {
                    actions.Add(new TopologyImportAction(TopologyImportActionType.CreateSubscription, subscriptionPath, $"Create subscription {subscriptionPath}"));
                }
                else if (SubscriptionNeedsUpdate(existingSubscription!, subscription))
                {
                    actions.Add(new TopologyImportAction(TopologyImportActionType.UpdateSubscription, subscriptionPath, $"Update subscription {subscriptionPath}"));
                }
                else
                {
                    actions.Add(new TopologyImportAction(TopologyImportActionType.Skip, subscriptionPath, $"Subscription {subscriptionPath} already matches"));
                }

                var existingRules = subscriptionExists
                    ? (await operations.GetSubscriptionRulesAsync(topic.Name, subscription.Name, ct)).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, SubscriptionRuleInfo>(StringComparer.OrdinalIgnoreCase);

                foreach (var rule in subscription.Rules.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var rulePath = $"{subscriptionPath}/{rule.Name}";
                    actions.Add(existingRules.ContainsKey(rule.Name)
                        ? new TopologyImportAction(TopologyImportActionType.Skip, rulePath, $"Rule {rulePath} already exists")
                        : new TopologyImportAction(TopologyImportActionType.CreateRule, rulePath, $"Create rule {rulePath}"));
                }
            }
        }

        return new TopologyImportPlan(actions);
    }

    public async Task ApplyImportPlanAsync(
        IServiceBusOperations operations,
        NamespaceTopologyDocument document,
        TopologyImportPlan plan,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var action in plan.Actions.Where(a => a.ActionType != TopologyImportActionType.Skip))
        {
            switch (action.ActionType)
            {
                case TopologyImportActionType.CreateQueue:
                    await operations.CreateQueueAsync(ToQueueCreationOptions(document.Queues.Single(q => q.Name == action.EntityPath)), ct);
                    break;
                case TopologyImportActionType.UpdateQueue:
                    await operations.UpdateQueueAsync(action.EntityPath, ToQueueUpdateOptions(document.Queues.Single(q => q.Name == action.EntityPath)), ct);
                    break;
                case TopologyImportActionType.CreateTopic:
                    await operations.CreateTopicAsync(ToTopicCreationOptions(document.Topics.Single(t => t.Name == action.EntityPath)), ct);
                    break;
                case TopologyImportActionType.UpdateTopic:
                    await operations.UpdateTopicAsync(action.EntityPath, ToTopicUpdateOptions(document.Topics.Single(t => t.Name == action.EntityPath)), ct);
                    break;
                case TopologyImportActionType.CreateSubscription:
                    {
                        var (topic, subscription) = FindSubscription(document, action.EntityPath);
                        await operations.CreateSubscriptionAsync(topic.Name, new SubscriptionCreationOptions(subscription.Name, subscription.RequiresSession), ct);
                        await operations.UpdateSubscriptionAsync(topic.Name, subscription.Name, ToSubscriptionUpdateOptions(subscription), ct);
                        break;
                    }
                case TopologyImportActionType.UpdateSubscription:
                    {
                        var (topic, subscription) = FindSubscription(document, action.EntityPath);
                        await operations.UpdateSubscriptionAsync(topic.Name, subscription.Name, ToSubscriptionUpdateOptions(subscription), ct);
                        break;
                    }
                case TopologyImportActionType.CreateRule:
                    {
                        var (topic, subscription, rule) = FindRule(document, action.EntityPath);
                        await operations.CreateSubscriptionRuleAsync(topic.Name, subscription.Name, ToRuleCreationOptions(rule), ct);
                        break;
                    }
            }
        }
    }

    private static bool QueueNeedsUpdate(QueueInfo existing, QueueTopology desired) =>
        existing.DefaultMessageTtl != desired.DefaultMessageTimeToLive ||
        existing.LockDuration != desired.LockDuration;

    private static bool SubscriptionNeedsUpdate(SubscriptionInfo existing, SubscriptionTopology desired) =>
        existing.LockDuration != desired.LockDuration ||
        existing.MaxDeliveryCount != desired.MaxDeliveryCount ||
        existing.DefaultMessageTimeToLive != desired.DefaultMessageTimeToLive ||
        existing.AutoDeleteOnIdle != desired.AutoDeleteOnIdle ||
        existing.ForwardTo != desired.ForwardTo ||
        existing.ForwardDeadLetteredMessagesTo != desired.ForwardDeadLetteredMessagesTo ||
        existing.EnableBatchedOperations != desired.EnableBatchedOperations ||
        existing.DeadLetteringOnMessageExpiration != desired.DeadLetteringOnMessageExpiration;

    private static QueueCreationOptions ToQueueCreationOptions(QueueTopology queue) => new(
        queue.Name,
        queue.RequiresSession,
        queue.DefaultMessageTimeToLive,
        queue.LockDuration,
        EnableBatchedOperations: queue.EnableBatchedOperations);

    private static QueueUpdateOptions ToQueueUpdateOptions(QueueTopology queue) => new(
        queue.LockDuration,
        queue.DefaultMessageTimeToLive,
        queue.DeadLetteringOnMessageExpiration,
        queue.EnableBatchedOperations);

    private static TopicCreationOptions ToTopicCreationOptions(TopicTopology topic) => new(
        topic.Name,
        topic.DefaultMessageTimeToLive,
        EnableBatchedOperations: topic.EnableBatchedOperations);

    private static TopicUpdateOptions ToTopicUpdateOptions(TopicTopology topic) => new(
        topic.DefaultMessageTimeToLive,
        topic.EnableBatchedOperations);

    private static SubscriptionUpdateOptions ToSubscriptionUpdateOptions(SubscriptionTopology subscription) => new(
        subscription.LockDuration,
        subscription.MaxDeliveryCount,
        subscription.DefaultMessageTimeToLive,
        subscription.AutoDeleteOnIdle,
        subscription.ForwardTo,
        subscription.ForwardDeadLetteredMessagesTo,
        subscription.EnableBatchedOperations,
        subscription.DeadLetteringOnMessageExpiration);

    private static SubscriptionRuleCreationOptions ToRuleCreationOptions(RuleTopology rule) => new(
        rule.Name,
        rule.SqlExpression,
        rule.FilterType,
        rule.ActionExpression,
        rule.CorrelationProperties);

    private static (TopicTopology Topic, SubscriptionTopology Subscription) FindSubscription(NamespaceTopologyDocument document, string path)
    {
        var parts = path.Split('/', 2);
        var topic = document.Topics.Single(t => t.Name == parts[0]);
        return (topic, topic.Subscriptions.Single(s => s.Name == parts[1]));
    }

    private static (TopicTopology Topic, SubscriptionTopology Subscription, RuleTopology Rule) FindRule(NamespaceTopologyDocument document, string path)
    {
        var parts = path.Split('/', 3);
        var topic = document.Topics.Single(t => t.Name == parts[0]);
        var subscription = topic.Subscriptions.Single(s => s.Name == parts[1]);
        return (topic, subscription, subscription.Rules.Single(r => r.Name == parts[2]));
    }
}
