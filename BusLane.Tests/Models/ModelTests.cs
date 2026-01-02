using BusLane.Models;
using FluentAssertions;

namespace BusLane.Tests.Models;

public class AlertRuleTests
{
    [Fact]
    public void AlertRule_WithSameValues_AreEqual()
    {
        // Arrange
        var rule1 = new AlertRule("id1", "Test", AlertType.DeadLetterThreshold, AlertSeverity.Warning, 10, true, null);
        var rule2 = new AlertRule("id1", "Test", AlertType.DeadLetterThreshold, AlertSeverity.Warning, 10, true, null);

        // Assert
        rule1.Should().Be(rule2);
    }

    [Fact]
    public void AlertRule_WithDifferentId_AreNotEqual()
    {
        // Arrange
        var rule1 = new AlertRule("id1", "Test", AlertType.DeadLetterThreshold, AlertSeverity.Warning, 10, true, null);
        var rule2 = new AlertRule("id2", "Test", AlertType.DeadLetterThreshold, AlertSeverity.Warning, 10, true, null);

        // Assert
        rule1.Should().NotBe(rule2);
    }

    [Fact]
    public void AlertRule_WithExpression_CreatesUpdatedCopy()
    {
        // Arrange
        var original = new AlertRule("id1", "Original", AlertType.DeadLetterThreshold, AlertSeverity.Warning, 10, true, null);

        // Act
        var updated = original with { Name = "Updated", Threshold = 20 };

        // Assert
        updated.Id.Should().Be(original.Id);
        updated.Name.Should().Be("Updated");
        updated.Threshold.Should().Be(20);
        original.Name.Should().Be("Original"); // Original unchanged
    }

    [Fact]
    public void AlertRule_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var rule = new AlertRule("id", "Test", AlertType.MessageCountThreshold, AlertSeverity.Info, 100);

        // Assert
        rule.IsEnabled.Should().BeTrue();
        rule.EntityPattern.Should().BeNull();
    }
}

public class AlertEventTests
{
    [Fact]
    public void AlertEvent_WithSameValues_AreEqual()
    {
        // Arrange
        var rule = new AlertRule("r1", "Test", AlertType.DeadLetterThreshold, AlertSeverity.Warning, 10, true, null);
        var timestamp = DateTimeOffset.UtcNow;
        
        var event1 = new AlertEvent("e1", rule, "queue1", "Queue", 15, timestamp, false);
        var event2 = new AlertEvent("e1", rule, "queue1", "Queue", 15, timestamp, false);

        // Assert
        event1.Should().Be(event2);
    }

    [Fact]
    public void AlertEvent_DefaultIsAcknowledged_IsFalse()
    {
        // Arrange
        var rule = new AlertRule("r1", "Test", AlertType.DeadLetterThreshold, AlertSeverity.Warning, 10, true, null);

        // Act
        var alertEvent = new AlertEvent("e1", rule, "queue1", "Queue", 15, DateTimeOffset.UtcNow);

        // Assert
        alertEvent.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public void AlertEvent_WithExpression_CreatesUpdatedCopy()
    {
        // Arrange
        var rule = new AlertRule("r1", "Test", AlertType.DeadLetterThreshold, AlertSeverity.Warning, 10, true, null);
        var original = new AlertEvent("e1", rule, "queue1", "Queue", 15, DateTimeOffset.UtcNow, false);

        // Act
        var acknowledged = original with { IsAcknowledged = true };

        // Assert
        acknowledged.IsAcknowledged.Should().BeTrue();
        original.IsAcknowledged.Should().BeFalse();
    }
}

public class MetricDataPointTests
{
    [Fact]
    public void MetricDataPoint_WithSameValues_AreEqual()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var point1 = new MetricDataPoint(timestamp, "queue1", "ActiveMessageCount", 42);
        var point2 = new MetricDataPoint(timestamp, "queue1", "ActiveMessageCount", 42);

        // Assert
        point1.Should().Be(point2);
    }

    [Fact]
    public void MetricDataPoint_ContainsExpectedData()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);

        // Act
        var point = new MetricDataPoint(timestamp, "test-queue", "DeadLetterCount", 100.5);

        // Assert
        point.Timestamp.Should().Be(timestamp);
        point.EntityName.Should().Be("test-queue");
        point.MetricName.Should().Be("DeadLetterCount");
        point.Value.Should().Be(100.5);
    }
}

public class QueueInfoTests
{
    [Fact]
    public void QueueInfo_WithSameValues_AreEqual()
    {
        // Arrange
        var accessedAt = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromDays(7);
        var lockDuration = TimeSpan.FromSeconds(30);

        var queue1 = new QueueInfo("queue1", 100, 80, 20, 5, 1024, accessedAt, false, ttl, lockDuration);
        var queue2 = new QueueInfo("queue1", 100, 80, 20, 5, 1024, accessedAt, false, ttl, lockDuration);

        // Assert
        queue1.Should().Be(queue2);
    }

    [Fact]
    public void QueueInfo_ImplementsIMessageEntity()
    {
        // Arrange & Act
        var queue = new QueueInfo("queue1", 100, 80, 20, 5, 1024, null, false, TimeSpan.FromDays(7), TimeSpan.FromSeconds(30));

        // Assert
        queue.Should().BeAssignableTo<BusLane.Models.Entities.IMessageEntity>();
    }

    [Fact]
    public void QueueInfo_ContainsExpectedProperties()
    {
        // Arrange
        var accessedAt = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
        var ttl = TimeSpan.FromDays(14);
        var lockDuration = TimeSpan.FromMinutes(1);

        // Act
        var queue = new QueueInfo(
            "my-queue",
            MessageCount: 150,
            ActiveMessageCount: 100,
            DeadLetterCount: 50,
            ScheduledCount: 10,
            SizeInBytes: 2048,
            AccessedAt: accessedAt,
            RequiresSession: true,
            DefaultMessageTtl: ttl,
            LockDuration: lockDuration
        );

        // Assert
        queue.Name.Should().Be("my-queue");
        queue.MessageCount.Should().Be(150);
        queue.ActiveMessageCount.Should().Be(100);
        queue.DeadLetterCount.Should().Be(50);
        queue.ScheduledCount.Should().Be(10);
        queue.SizeInBytes.Should().Be(2048);
        queue.AccessedAt.Should().Be(accessedAt);
        queue.RequiresSession.Should().BeTrue();
        queue.DefaultMessageTtl.Should().Be(ttl);
        queue.LockDuration.Should().Be(lockDuration);
    }
}

public class SubscriptionInfoTests
{
    [Fact]
    public void SubscriptionInfo_WithSameValues_AreEqual()
    {
        // Arrange
        var accessedAt = DateTimeOffset.UtcNow;
        var sub1 = new SubscriptionInfo("sub1", "topic1", 100, 80, 20, accessedAt, false);
        var sub2 = new SubscriptionInfo("sub1", "topic1", 100, 80, 20, accessedAt, false);

        // Assert
        sub1.Should().Be(sub2);
    }

    [Fact]
    public void SubscriptionInfo_ImplementsIMessageEntity()
    {
        // Arrange & Act
        var sub = new SubscriptionInfo("sub1", "topic1", 100, 80, 20, null, false);

        // Assert
        sub.Should().BeAssignableTo<BusLane.Models.Entities.IMessageEntity>();
    }

    [Fact]
    public void SubscriptionInfo_ContainsExpectedProperties()
    {
        // Arrange
        var accessedAt = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);

        // Act
        var sub = new SubscriptionInfo(
            Name: "my-subscription",
            TopicName: "my-topic",
            MessageCount: 200,
            ActiveMessageCount: 150,
            DeadLetterCount: 50,
            AccessedAt: accessedAt,
            RequiresSession: true
        );

        // Assert
        sub.Name.Should().Be("my-subscription");
        sub.TopicName.Should().Be("my-topic");
        sub.MessageCount.Should().Be(200);
        sub.ActiveMessageCount.Should().Be(150);
        sub.DeadLetterCount.Should().Be(50);
        sub.AccessedAt.Should().Be(accessedAt);
        sub.RequiresSession.Should().BeTrue();
    }
}

public class SavedConnectionTests
{
    [Fact]
    public void SavedConnection_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var connection = new SavedConnection();

        // Assert
        connection.Id.Should().BeNullOrEmpty();
        connection.Name.Should().BeEmpty();
        connection.ConnectionString.Should().BeEmpty();
        connection.IsFavorite.Should().BeFalse();
        connection.Environment.Should().Be(ConnectionEnvironment.None);
    }

    [Fact]
    public void SavedConnection_CanSetAllProperties()
    {
        // Arrange
        var createdAt = new DateTime(2026, 1, 1);

        // Act
        var connection = new SavedConnection
        {
            Id = "custom-id",
            Name = "My Connection",
            ConnectionString = "Endpoint=...",
            Type = ConnectionType.Topic,
            EntityName = "my-topic",
            CreatedAt = createdAt,
            IsFavorite = true,
            Environment = ConnectionEnvironment.Development
        };

        // Assert
        connection.Id.Should().Be("custom-id");
        connection.Name.Should().Be("My Connection");
        connection.ConnectionString.Should().Be("Endpoint=...");
        connection.Type.Should().Be(ConnectionType.Topic);
        connection.EntityName.Should().Be("my-topic");
        connection.CreatedAt.Should().Be(createdAt);
        connection.IsFavorite.Should().BeTrue();
        connection.Environment.Should().Be(ConnectionEnvironment.Development);
    }
}

public class SavedMessageTests
{
    [Fact]
    public void SavedMessage_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var message = new SavedMessage();

        // Assert
        message.Id.Should().NotBeNullOrEmpty();
        message.Name.Should().BeEmpty();
        message.Body.Should().BeEmpty();
        message.CustomProperties.Should().BeEmpty();
        message.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SavedMessage_CanSetAllProperties()
    {
        // Arrange & Act
        var message = new SavedMessage
        {
            Id = "msg-id",
            Name = "Test Message",
            Body = "{ \"test\": true }",
            ContentType = "application/json",
            CorrelationId = "corr-123",
            MessageId = "msg-123",
            SessionId = "session-123",
            Subject = "Test Subject",
            To = "destination",
            ReplyTo = "reply-queue",
            ReplyToSessionId = "reply-session",
            PartitionKey = "partition1",
            TimeToLive = TimeSpan.FromMinutes(30),
            ScheduledEnqueueTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            CustomProperties = new Dictionary<string, string> { { "key1", "value1" } }
        };

        // Assert
        message.ContentType.Should().Be("application/json");
        message.CorrelationId.Should().Be("corr-123");
        message.TimeToLive.Should().Be(TimeSpan.FromMinutes(30));
        message.CustomProperties.Should().ContainKey("key1");
    }
}

public class CustomPropertyTests
{
    [Fact]
    public void CustomProperty_DefaultValues_AreEmptyStrings()
    {
        // Arrange & Act
        var prop = new CustomProperty();

        // Assert
        prop.Key.Should().BeEmpty();
        prop.Value.Should().BeEmpty();
    }

    [Fact]
    public void CustomProperty_CanSetProperties()
    {
        // Arrange & Act
        var prop = new CustomProperty
        {
            Key = "MyKey",
            Value = "MyValue"
        };

        // Assert
        prop.Key.Should().Be("MyKey");
        prop.Value.Should().Be("MyValue");
    }
}

