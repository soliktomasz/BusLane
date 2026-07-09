namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;
using Xunit;

public class NamespaceTopologyServiceTests
{
    [Fact]
    public void DeserializeDocument_WithUnsupportedVersion_Throws()
    {
        // Arrange
        var json = """
        {
          "schemaVersion": 999,
          "exportedAt": "2026-07-08T00:00:00Z",
          "queues": [],
          "topics": []
        }
        """;

        // Act
        var act = () => NamespaceTopologySerializer.Deserialize(json);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported namespace topology schema version*");
    }

    [Fact]
    public void SerializeDocument_DoesNotIncludeSecretsOrMessages()
    {
        // Arrange
        var document = new NamespaceTopologyDocument(
            SchemaVersion: 1,
            ExportedAt: DateTimeOffset.Parse("2026-07-08T00:00:00Z"),
            Queues:
            [
                new QueueTopology(
                    "orders",
                    RequiresSession: true,
                    DefaultMessageTimeToLive: TimeSpan.FromDays(14),
                    LockDuration: TimeSpan.FromMinutes(1),
                    DeadLetteringOnMessageExpiration: null,
                    EnableBatchedOperations: true)
            ],
            Topics: []);

        // Act
        var json = NamespaceTopologySerializer.Serialize(document);

        // Assert
        json.Should().Contain("\"schemaVersion\": 1");
        json.Should().Contain("orders");
        var normalizedJson = json.ToLowerInvariant();
        normalizedJson.Should().NotContain("connectionstring");
        normalizedJson.Should().NotContain("sharedaccesskey");
        normalizedJson.Should().NotContain("messages");
    }

    [Fact]
    public async Task ExportAsync_BuildsDocumentFromQueuesTopicsSubscriptionsAndRules()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns([
            new QueueInfo("orders", 0, 0, 0, 0, 0, null, true, TimeSpan.FromDays(7), TimeSpan.FromSeconds(30))
        ]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns([
            new TopicInfo("events", 0, 1, null, TimeSpan.FromDays(14))
        ]);
        operations.GetSubscriptionsAsync("events", Arg.Any<CancellationToken>()).Returns([
            new SubscriptionInfo(
                "processor",
                "events",
                0,
                0,
                0,
                null,
                false,
                TimeSpan.FromSeconds(45),
                10,
                TimeSpan.FromDays(5),
                TimeSpan.FromDays(30),
                "forward",
                "dlq-forward",
                true,
                true)
        ]);
        operations.GetSubscriptionRulesAsync("events", "processor", Arg.Any<CancellationToken>())
            .Returns([
                new SubscriptionRuleInfo("important", SubscriptionRuleFilterType.Sql, "priority = 'high'", "priority = 'high'")
            ]);
        var sut = new NamespaceTopologyService(() => DateTimeOffset.Parse("2026-07-08T00:00:00Z"));

        // Act
        var document = await sut.ExportAsync(operations);

        // Assert
        document.SchemaVersion.Should().Be(1);
        document.Queues.Should().ContainSingle(q => q.Name == "orders" && q.RequiresSession);
        var subscription = document.Topics.Single().Subscriptions.Single();
        subscription.Name.Should().Be("processor");
        subscription.Rules.Should().ContainSingle(r => r.Name == "important" && r.FilterType == SubscriptionRuleFilterType.Sql);
    }

    [Fact]
    public async Task BuildImportPlanAsync_WithMissingEntities_CreatesNonDestructiveActionsOnly()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<QueueInfo>());
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TopicInfo>());

        var document = new NamespaceTopologyDocument(
            1,
            DateTimeOffset.Parse("2026-07-08T00:00:00Z"),
            [new QueueTopology("orders", false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1), null, true)],
            [
                new TopicTopology(
                    "events",
                    TimeSpan.FromDays(14),
                    true,
                    [
                        new SubscriptionTopology(
                            "processor",
                            false,
                            TimeSpan.FromMinutes(1),
                            10,
                            TimeSpan.FromDays(14),
                            null,
                            null,
                            null,
                            true,
                            true,
                            [
                                new RuleTopology("important", SubscriptionRuleFilterType.Sql, "priority = 'high'", "priority = 'high'", null, null)
                            ])
                    ])
            ]);
        var sut = new NamespaceTopologyService();

        // Act
        var plan = await sut.BuildImportPlanAsync(operations, document);

        // Assert
        plan.Actions.Should().Contain(a => a.ActionType == TopologyImportActionType.CreateQueue && a.EntityPath == "orders");
        plan.Actions.Should().Contain(a => a.ActionType == TopologyImportActionType.CreateTopic && a.EntityPath == "events");
        plan.Actions.Should().Contain(a => a.ActionType == TopologyImportActionType.CreateSubscription && a.EntityPath == "events/processor");
        plan.Actions.Should().Contain(a => a.ActionType == TopologyImportActionType.CreateRule && a.EntityPath == "events/processor/important");
        plan.Actions.Should().NotContain(a => a.ActionType.ToString().Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildImportPlanAsync_WithForwardPathCaseOnlyDifference_SkipsSubscriptionUpdate()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<QueueInfo>());
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns([new TopicInfo("events", 0, 1, null, TimeSpan.FromDays(14))]);
        operations.GetSubscriptionsAsync("events", Arg.Any<CancellationToken>()).Returns([
            new SubscriptionInfo(
                "processor",
                "events",
                0,
                0,
                0,
                null,
                false,
                TimeSpan.FromMinutes(1),
                10,
                TimeSpan.FromDays(14),
                null,
                "Forward/Queue",
                "Forward/Dlq",
                true,
                true)
        ]);
        operations.GetSubscriptionRulesAsync("events", "processor", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SubscriptionRuleInfo>());
        var document = new NamespaceTopologyDocument(
            1,
            DateTimeOffset.UtcNow,
            [],
            [
                new TopicTopology(
                    "events",
                    TimeSpan.FromDays(14),
                    null,
                    [
                        new SubscriptionTopology(
                            "processor",
                            false,
                            TimeSpan.FromMinutes(1),
                            10,
                            TimeSpan.FromDays(14),
                            null,
                            "forward/queue",
                            "forward/dlq",
                            true,
                            true,
                            [])
                    ])
            ]);
        var sut = new NamespaceTopologyService();

        // Act
        var plan = await sut.BuildImportPlanAsync(operations, document);

        // Assert
        plan.Actions.Should().Contain(a =>
            a.ActionType == TopologyImportActionType.Skip &&
            a.EntityPath == "events/processor");
        plan.Actions.Should().NotContain(a =>
            a.ActionType == TopologyImportActionType.UpdateSubscription &&
            a.EntityPath == "events/processor");
    }

    [Fact]
    public async Task BuildImportPlanAsync_WithChangedForwardPath_AddsSubscriptionUpdate()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<QueueInfo>());
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns([new TopicInfo("events", 0, 1, null, TimeSpan.FromDays(14))]);
        operations.GetSubscriptionsAsync("events", Arg.Any<CancellationToken>()).Returns([
            new SubscriptionInfo(
                "processor",
                "events",
                0,
                0,
                0,
                null,
                false,
                TimeSpan.FromMinutes(1),
                10,
                TimeSpan.FromDays(14),
                null,
                "forward-a",
                null,
                true,
                true)
        ]);
        operations.GetSubscriptionRulesAsync("events", "processor", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SubscriptionRuleInfo>());
        var document = new NamespaceTopologyDocument(
            1,
            DateTimeOffset.UtcNow,
            [],
            [
                new TopicTopology(
                    "events",
                    TimeSpan.FromDays(14),
                    null,
                    [
                        new SubscriptionTopology(
                            "processor",
                            false,
                            TimeSpan.FromMinutes(1),
                            10,
                            TimeSpan.FromDays(14),
                            null,
                            "forward-b",
                            null,
                            true,
                            true,
                            [])
                    ])
            ]);
        var sut = new NamespaceTopologyService();

        // Act
        var plan = await sut.BuildImportPlanAsync(operations, document);

        // Assert
        plan.Actions.Should().Contain(a =>
            a.ActionType == TopologyImportActionType.UpdateSubscription &&
            a.EntityPath == "events/processor");
    }

    [Fact]
    public async Task BuildImportPlanAsync_WithMatchingQueue_AddsSkip()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns([
            new QueueInfo("orders", 0, 0, 0, 0, 0, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1))
        ]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TopicInfo>());
        var document = new NamespaceTopologyDocument(
            1,
            DateTimeOffset.UtcNow,
            [new QueueTopology("orders", false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1), null, null)],
            []);
        var sut = new NamespaceTopologyService();

        // Act
        var plan = await sut.BuildImportPlanAsync(operations, document);

        // Assert
        plan.Actions.Should().ContainSingle(a =>
            a.ActionType == TopologyImportActionType.Skip &&
            a.EntityPath == "orders");
    }

    [Fact]
    public async Task BuildImportPlanAsync_WithChangedQueue_AddsQueueUpdate()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns([
            new QueueInfo("orders", 0, 0, 0, 0, 0, null, false, TimeSpan.FromDays(7), TimeSpan.FromMinutes(1))
        ]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TopicInfo>());
        var document = new NamespaceTopologyDocument(
            1,
            DateTimeOffset.UtcNow,
            [new QueueTopology("orders", false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1), null, null)],
            []);
        var sut = new NamespaceTopologyService();

        // Act
        var plan = await sut.BuildImportPlanAsync(operations, document);

        // Assert
        plan.Actions.Should().ContainSingle(a =>
            a.ActionType == TopologyImportActionType.UpdateQueue &&
            a.EntityPath == "orders");
    }

    [Fact]
    public void SerializeDeserialize_RoundTripsTopologyDocument()
    {
        // Arrange
        var document = CreateTopologyDocument();

        // Act
        var json = NamespaceTopologySerializer.Serialize(document);
        var result = NamespaceTopologySerializer.Deserialize(json);

        // Assert
        result.Should().BeEquivalentTo(document);
    }

    [Theory]
    [InlineData(TopologyImportActionType.CreateQueue)]
    [InlineData(TopologyImportActionType.UpdateQueue)]
    [InlineData(TopologyImportActionType.CreateTopic)]
    [InlineData(TopologyImportActionType.UpdateTopic)]
    [InlineData(TopologyImportActionType.CreateSubscription)]
    [InlineData(TopologyImportActionType.UpdateSubscription)]
    [InlineData(TopologyImportActionType.CreateRule)]
    public async Task ApplyImportPlanAsync_AppliesSupportedAction(TopologyImportActionType actionType)
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var document = CreateTopologyDocument();
        var entityPath = actionType switch
        {
            TopologyImportActionType.CreateQueue or TopologyImportActionType.UpdateQueue => "orders",
            TopologyImportActionType.CreateTopic or TopologyImportActionType.UpdateTopic => "events",
            TopologyImportActionType.CreateSubscription or TopologyImportActionType.UpdateSubscription => "events/processor",
            TopologyImportActionType.CreateRule => "events/processor/important",
            _ => throw new InvalidOperationException("Unsupported test action.")
        };
        var plan = new TopologyImportPlan([new TopologyImportAction(actionType, entityPath, actionType.ToString())]);
        var sut = new NamespaceTopologyService();

        // Act
        await sut.ApplyImportPlanAsync(operations, document, plan);

        // Assert
        switch (actionType)
        {
            case TopologyImportActionType.CreateQueue:
                await operations.Received(1).CreateQueueAsync(
                    Arg.Is<QueueCreationOptions>(options => options.Name == "orders"),
                    Arg.Any<CancellationToken>());
                break;
            case TopologyImportActionType.UpdateQueue:
                await operations.Received(1).UpdateQueueAsync(
                    "orders",
                    Arg.Any<QueueUpdateOptions>(),
                    Arg.Any<CancellationToken>());
                break;
            case TopologyImportActionType.CreateTopic:
                await operations.Received(1).CreateTopicAsync(
                    Arg.Is<TopicCreationOptions>(options => options.Name == "events"),
                    Arg.Any<CancellationToken>());
                break;
            case TopologyImportActionType.UpdateTopic:
                await operations.Received(1).UpdateTopicAsync(
                    "events",
                    Arg.Any<TopicUpdateOptions>(),
                    Arg.Any<CancellationToken>());
                break;
            case TopologyImportActionType.CreateSubscription:
                await operations.Received(1).CreateSubscriptionAsync(
                    "events",
                    Arg.Is<SubscriptionCreationOptions>(options => options.Name == "processor"),
                    Arg.Any<CancellationToken>());
                await operations.Received(1).UpdateSubscriptionAsync(
                    "events",
                    "processor",
                    Arg.Any<SubscriptionUpdateOptions>(),
                    Arg.Any<CancellationToken>());
                break;
            case TopologyImportActionType.UpdateSubscription:
                await operations.Received(1).UpdateSubscriptionAsync(
                    "events",
                    "processor",
                    Arg.Any<SubscriptionUpdateOptions>(),
                    Arg.Any<CancellationToken>());
                break;
            case TopologyImportActionType.CreateRule:
                await operations.Received(1).CreateSubscriptionRuleAsync(
                    "events",
                    "processor",
                    Arg.Is<SubscriptionRuleCreationOptions>(options => options.Name == "important"),
                    Arg.Any<CancellationToken>());
                break;
        }
    }

    [Fact]
    public async Task ApplyImportPlanAsync_WithUnknownActionType_Throws()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var document = new NamespaceTopologyDocument(1, DateTimeOffset.UtcNow, [], []);
        var plan = new TopologyImportPlan(
        [
            new TopologyImportAction((TopologyImportActionType)999, "unknown", "Unknown")
        ]);
        var sut = new NamespaceTopologyService();

        // Act
        var act = () => sut.ApplyImportPlanAsync(operations, document, plan);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unsupported topology import action*");
    }

    private static NamespaceTopologyDocument CreateTopologyDocument() =>
        new(
            1,
            DateTimeOffset.Parse("2026-07-08T00:00:00Z"),
            [new QueueTopology("orders", false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1), null, true)],
            [
                new TopicTopology(
                    "events",
                    TimeSpan.FromDays(14),
                    true,
                    [
                        new SubscriptionTopology(
                            "processor",
                            false,
                            TimeSpan.FromMinutes(1),
                            10,
                            TimeSpan.FromDays(14),
                            null,
                            "forward",
                            "forward-dlq",
                            true,
                            true,
                            [
                                new RuleTopology("important", SubscriptionRuleFilterType.Sql, "priority = 'high'", "priority = 'high'", null, null)
                            ])
                    ])
            ]);
}
