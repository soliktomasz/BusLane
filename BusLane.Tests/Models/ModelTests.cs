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

public class MessageInfoTests
{
    [Fact]
    public void MessageInfo_WithSameValues_AreEqual()
    {
        // Arrange
        var props = new Dictionary<string, object> { { "key", "value" } };
        var enqueuedTime = DateTimeOffset.UtcNow;

        var msg1 = new MessageInfo("msg-1", "corr-1", "application/json", "body", enqueuedTime, null, 1, 0, null, props);
        var msg2 = new MessageInfo("msg-1", "corr-1", "application/json", "body", enqueuedTime, null, 1, 0, null, props);

        // Assert
        msg1.Should().Be(msg2);
    }

    [Fact]
    public void MessageInfo_BodyPreview_TruncatesLongBody()
    {
        // Arrange - Body longer than 200 chars
        var longBody = new string('A', 300);
        var msg = new MessageInfo("msg-1", null, null, longBody, DateTimeOffset.UtcNow, null, 1, 0, null, new Dictionary<string, object>());

        // Act & Assert
        msg.BodyPreview.Length.Should().BeLessThanOrEqualTo(201); // 200 chars + ellipsis
        msg.BodyPreview.Should().EndWith("…");
    }

    [Fact]
    public void MessageInfo_BodyPreview_PreservesShortBody()
    {
        // Arrange
        const string shortBody = "Short message body";
        var msg = new MessageInfo("msg-1", null, null, shortBody, DateTimeOffset.UtcNow, null, 1, 0, null, new Dictionary<string, object>());

        // Act & Assert
        msg.BodyPreview.Should().Be(shortBody);
    }

    [Fact]
    public void MessageInfo_BodyPreview_HandlesEmptyBody()
    {
        // Arrange
        var msg = new MessageInfo("msg-1", null, null, "", DateTimeOffset.UtcNow, null, 1, 0, null, new Dictionary<string, object>());

        // Act & Assert
        msg.BodyPreview.Should().BeEmpty();
    }

    [Fact]
    public void MessageInfo_BodyPreview_ReplacesNewlines()
    {
        // Arrange
        const string bodyWithNewlines = "Line1\nLine2\r\nLine3";
        var msg = new MessageInfo("msg-1", null, null, bodyWithNewlines, DateTimeOffset.UtcNow, null, 1, 0, null, new Dictionary<string, object>());

        // Act & Assert
        msg.BodyPreview.Should().NotContain("\n");
        msg.BodyPreview.Should().NotContain("\r");
    }

    [Fact]
    public void MessageInfo_ContainsAllProperties()
    {
        // Arrange
        var enqueuedTime = DateTimeOffset.UtcNow;
        var scheduledTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var lockedUntil = DateTimeOffset.UtcNow.AddMinutes(1);
        var props = new Dictionary<string, object> { { "custom", 123 } };

        // Act
        var msg = new MessageInfo(
            MessageId: "msg-123",
            CorrelationId: "corr-456",
            ContentType: "application/json",
            Body: "{ \"test\": true }",
            EnqueuedTime: enqueuedTime,
            ScheduledEnqueueTime: scheduledTime,
            SequenceNumber: 42,
            DeliveryCount: 3,
            SessionId: "session-1",
            Properties: props,
            Subject: "Test Subject",
            To: "destination",
            ReplyTo: "reply-queue",
            ReplyToSessionId: "reply-session",
            PartitionKey: "pk-1",
            TimeToLive: TimeSpan.FromHours(1),
            ExpiresAt: expiresAt,
            LockToken: "lock-token-123",
            LockedUntil: lockedUntil,
            DeadLetterSource: "original-queue",
            DeadLetterReason: "MaxDeliveryCountExceeded",
            DeadLetterErrorDescription: "Message delivery count exceeded"
        );

        // Assert
        msg.MessageId.Should().Be("msg-123");
        msg.CorrelationId.Should().Be("corr-456");
        msg.ContentType.Should().Be("application/json");
        msg.Body.Should().Be("{ \"test\": true }");
        msg.EnqueuedTime.Should().Be(enqueuedTime);
        msg.ScheduledEnqueueTime.Should().Be(scheduledTime);
        msg.SequenceNumber.Should().Be(42);
        msg.DeliveryCount.Should().Be(3);
        msg.SessionId.Should().Be("session-1");
        msg.Properties.Should().ContainKey("custom");
        msg.Subject.Should().Be("Test Subject");
        msg.To.Should().Be("destination");
        msg.ReplyTo.Should().Be("reply-queue");
        msg.ReplyToSessionId.Should().Be("reply-session");
        msg.PartitionKey.Should().Be("pk-1");
        msg.TimeToLive.Should().Be(TimeSpan.FromHours(1));
        msg.ExpiresAt.Should().Be(expiresAt);
        msg.LockToken.Should().Be("lock-token-123");
        msg.LockedUntil.Should().Be(lockedUntil);
        msg.DeadLetterSource.Should().Be("original-queue");
        msg.DeadLetterReason.Should().Be("MaxDeliveryCountExceeded");
        msg.DeadLetterErrorDescription.Should().Be("Message delivery count exceeded");
    }
}

public class TopicInfoTests
{
    [Fact]
    public void TopicInfo_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var topic = new TopicInfo();

        // Assert
        topic.Name.Should().BeEmpty();
        topic.SizeInBytes.Should().Be(0);
        topic.SubscriptionCount.Should().Be(0);
        topic.Subscriptions.Should().BeEmpty();
        topic.SubscriptionsLoaded.Should().BeFalse();
        topic.IsLoadingSubscriptions.Should().BeFalse();
    }

    [Fact]
    public void TopicInfo_ParameterizedConstructor_SetsAllProperties()
    {
        // Arrange
        var accessedAt = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromDays(7);

        // Act
        var topic = new TopicInfo("my-topic", 2048, 5, accessedAt, ttl);

        // Assert
        topic.Name.Should().Be("my-topic");
        topic.SizeInBytes.Should().Be(2048);
        topic.SubscriptionCount.Should().Be(5);
        topic.AccessedAt.Should().Be(accessedAt);
        topic.DefaultMessageTtl.Should().Be(ttl);
    }

    [Fact]
    public void TopicInfo_DisplayStatus_WhenLoadingSubscriptions_ReturnsLoading()
    {
        // Arrange
        var topic = new TopicInfo("my-topic", 1024, 3, null, TimeSpan.FromDays(1))
        {
            IsLoadingSubscriptions = true
        };

        // Act & Assert
        topic.DisplayStatus.Should().Be("Loading...");
    }

    [Fact]
    public void TopicInfo_DisplayStatus_WhenSubscriptionsLoaded_ReturnsCount()
    {
        // Arrange
        var topic = new TopicInfo("my-topic", 1024, 3, null, TimeSpan.FromDays(1))
        {
            SubscriptionsLoaded = true
        };
        topic.Subscriptions.Add(new SubscriptionInfo("sub1", "my-topic", 10, 10, 0, null, false));
        topic.Subscriptions.Add(new SubscriptionInfo("sub2", "my-topic", 20, 20, 0, null, false));

        // Act & Assert
        topic.DisplayStatus.Should().Be("2 subscription(s)");
    }

    [Fact]
    public void TopicInfo_DisplayStatus_WhenNotLoaded_ReturnsClickToExpand()
    {
        // Arrange
        var topic = new TopicInfo("my-topic", 1024, 3, null, TimeSpan.FromDays(1));

        // Act & Assert
        topic.DisplayStatus.Should().Be("Click to expand");
    }

    [Fact]
    public void TopicInfo_SettingIsLoadingSubscriptions_NotifiesPropertyChanged()
    {
        // Arrange
        var topic = new TopicInfo("my-topic", 1024, 3, null, TimeSpan.FromDays(1));
        var propertyChangedEvents = new List<string>();
        topic.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName!);

        // Act
        topic.IsLoadingSubscriptions = true;

        // Assert
        propertyChangedEvents.Should().Contain("IsLoadingSubscriptions");
        propertyChangedEvents.Should().Contain("DisplayStatus");
    }

    [Fact]
    public void TopicInfo_SettingSubscriptionsLoaded_NotifiesPropertyChanged()
    {
        // Arrange
        var topic = new TopicInfo("my-topic", 1024, 3, null, TimeSpan.FromDays(1));
        var propertyChangedEvents = new List<string>();
        topic.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName!);

        // Act
        topic.SubscriptionsLoaded = true;

        // Assert
        propertyChangedEvents.Should().Contain("SubscriptionsLoaded");
        propertyChangedEvents.Should().Contain("DisplayStatus");
    }
}

public class LiveStreamMessageTests
{
    [Fact]
    public void LiveStreamMessage_WithSameValues_AreEqual()
    {
        // Arrange
        var receivedAt = DateTimeOffset.UtcNow;
        var props = new Dictionary<string, object> { { "key", "value" } };

        var msg1 = new LiveStreamMessage("msg-1", "corr-1", "application/json", "body", receivedAt, "queue1", "Queue", null, 1, null, props);
        var msg2 = new LiveStreamMessage("msg-1", "corr-1", "application/json", "body", receivedAt, "queue1", "Queue", null, 1, null, props);

        // Assert
        msg1.Should().Be(msg2);
    }

    [Fact]
    public void LiveStreamMessage_ForQueue_HasCorrectEntityType()
    {
        // Arrange & Act
        var msg = new LiveStreamMessage(
            "msg-1", null, null, "body", DateTimeOffset.UtcNow,
            "my-queue", "Queue", null, 1, null, new Dictionary<string, object>());

        // Assert
        msg.EntityType.Should().Be("Queue");
        msg.EntityName.Should().Be("my-queue");
        msg.TopicName.Should().BeNull();
    }

    [Fact]
    public void LiveStreamMessage_ForSubscription_HasCorrectEntityTypeAndTopic()
    {
        // Arrange & Act
        var msg = new LiveStreamMessage(
            "msg-1", null, null, "body", DateTimeOffset.UtcNow,
            "my-subscription", "Subscription", "my-topic", 1, null, new Dictionary<string, object>());

        // Assert
        msg.EntityType.Should().Be("Subscription");
        msg.EntityName.Should().Be("my-subscription");
        msg.TopicName.Should().Be("my-topic");
    }

    [Fact]
    public void LiveStreamMessage_ContainsAllProperties()
    {
        // Arrange
        var receivedAt = DateTimeOffset.UtcNow;
        var props = new Dictionary<string, object> { { "custom", "value" }, { "number", 42 } };

        // Act
        var msg = new LiveStreamMessage(
            MessageId: "msg-123",
            CorrelationId: "corr-456",
            ContentType: "application/json",
            Body: "{ \"test\": true }",
            ReceivedAt: receivedAt,
            EntityName: "my-queue",
            EntityType: "Queue",
            TopicName: null,
            SequenceNumber: 100,
            SessionId: "session-1",
            Properties: props
        );

        // Assert
        msg.MessageId.Should().Be("msg-123");
        msg.CorrelationId.Should().Be("corr-456");
        msg.ContentType.Should().Be("application/json");
        msg.Body.Should().Be("{ \"test\": true }");
        msg.ReceivedAt.Should().Be(receivedAt);
        msg.EntityName.Should().Be("my-queue");
        msg.EntityType.Should().Be("Queue");
        msg.SequenceNumber.Should().Be(100);
        msg.SessionId.Should().Be("session-1");
        msg.Properties.Should().HaveCount(2);
    }
}

