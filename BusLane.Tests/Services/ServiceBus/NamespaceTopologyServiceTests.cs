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
}
