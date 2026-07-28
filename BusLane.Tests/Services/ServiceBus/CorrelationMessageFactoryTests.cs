namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;

public class CorrelationMessageFactoryTests
{
    [Fact]
    public void FromLoaded_CopiesMessageAndSourceContext()
    {
        // Arrange
        var properties = new Dictionary<string, object> { ["tenant"] = "north" };
        var message = new MessageInfo(
            "message-1",
            "corr-1",
            "application/json",
            "{}",
            DateTimeOffset.Parse("2026-07-28T10:00:00Z"),
            null,
            42,
            1,
            "session-1",
            properties,
            Subject: "created",
            To: "processor",
            ReplyTo: "replies",
            ReplyToSessionId: "reply-session",
            PartitionKey: "partition-1",
            TimeToLive: TimeSpan.FromMinutes(10));
        var context = new CorrelationSourceContext(
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            TopicName: null,
            SubscriptionName: null);

        // Act
        var result = CorrelationMessageFactory.FromLoaded(message, context);
        properties["tenant"] = "changed";

        // Assert
        result.Source.Should().Be(CorrelationMessageSource.Loaded);
        result.NamespaceName.Should().Be("demo.servicebus.windows.net");
        result.Environment.Should().Be(ConnectionEnvironment.Test);
        result.EntityName.Should().Be("orders");
        result.MessageId.Should().Be("message-1");
        result.CorrelationId.Should().Be("corr-1");
        result.SessionId.Should().Be("session-1");
        result.Subject.Should().Be("created");
        result.Properties["tenant"].Should().Be("north");
    }

    [Fact]
    public void FromLiveStream_UsesStreamEntityAndCopiesProperties()
    {
        // Arrange
        var properties = new Dictionary<string, object> { ["tenant"] = "north" };
        var message = new LiveStreamMessage(
            "message-1",
            "corr-1",
            "application/json",
            "{}",
            DateTimeOffset.Parse("2026-07-28T10:00:00Z"),
            "orders-sub",
            "Subscription",
            "orders-topic",
            42,
            "session-1",
            properties);
        var context = new CorrelationSourceContext(
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Production,
            "",
            "",
            TopicName: null,
            SubscriptionName: null);

        // Act
        var result = CorrelationMessageFactory.FromLiveStream(message, context);
        properties["tenant"] = "changed";

        // Assert
        result.Source.Should().Be(CorrelationMessageSource.LiveStream);
        result.EntityName.Should().Be("orders-sub");
        result.EntityType.Should().Be("Subscription");
        result.TopicName.Should().Be("orders-topic");
        result.SubscriptionName.Should().Be("orders-sub");
        result.Properties["tenant"].Should().Be("north");
    }
}
