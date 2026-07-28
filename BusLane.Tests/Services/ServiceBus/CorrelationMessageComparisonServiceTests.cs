namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;

public class CorrelationMessageComparisonServiceTests
{
    private readonly CorrelationMessageComparisonService _sut = new();

    [Fact]
    public void Compare_EquivalentJson_IgnoresFormatting()
    {
        // Arrange
        var first = CreateMessage(body: """{"order":{"id":42},"active":true}""");
        var second = CreateMessage(
            messageId: "message-2",
            sequenceNumber: 2,
            body: """
                  {
                    "order": { "id": 42 },
                    "active": true
                  }
                  """);

        // Act
        var result = _sut.Compare(first, second);

        // Assert
        result.Body.Kind.Should().Be(MessageBodyComparisonKind.Json);
        result.Body.IsChanged.Should().BeFalse();
        result.Body.First.Should().Be(result.Body.Second);
    }

    [Fact]
    public void Compare_ChangedJson_ReturnsNormalizedBodies()
    {
        // Arrange
        var first = CreateMessage(body: """{"status":"created"}""");
        var second = CreateMessage(
            messageId: "message-2",
            sequenceNumber: 2,
            body: """{"status":"shipped"}""");

        // Act
        var result = _sut.Compare(first, second);

        // Assert
        result.Body.Kind.Should().Be(MessageBodyComparisonKind.Json);
        result.Body.IsChanged.Should().BeTrue();
        result.Body.First.Should().Contain("\"created\"");
        result.Body.Second.Should().Contain("\"shipped\"");
    }

    [Fact]
    public void Compare_InvalidJson_FallsBackToPlainText()
    {
        // Arrange
        var first = CreateMessage(body: "{invalid");
        var second = CreateMessage(messageId: "message-2", sequenceNumber: 2, body: "plain text");

        // Act
        var result = _sut.Compare(first, second);

        // Assert
        result.Body.Kind.Should().Be(MessageBodyComparisonKind.Text);
        result.Body.IsChanged.Should().BeTrue();
        result.Body.First.Should().Be("{invalid");
        result.Body.Second.Should().Be("plain text");
    }

    [Fact]
    public void Compare_MetadataPropertiesAndTiming_ReturnsAllDifferences()
    {
        // Arrange
        var firstProperties = new Dictionary<string, object>
        {
            ["tenant"] = "north",
            ["removed"] = 1,
            ["same"] = true
        };
        var secondProperties = new Dictionary<string, object>
        {
            ["tenant"] = "south",
            ["added"] = 2,
            ["same"] = true
        };
        var first = CreateMessage(properties: firstProperties);
        var second = CreateMessage(
            messageId: "message-2",
            sequenceNumber: 2,
            enqueuedTime: DateTimeOffset.Parse("2026-07-28T09:00:05Z"),
            namespaceName: "other.servicebus.windows.net",
            environment: ConnectionEnvironment.Production,
            entityName: "orders-v2",
            source: CorrelationMessageSource.LiveStream,
            correlationId: "corr-2",
            sessionId: "session-2",
            contentType: "text/plain",
            subject: "updated",
            to: "target",
            replyTo: "reply",
            replyToSessionId: "reply-session",
            partitionKey: "partition",
            properties: secondProperties);

        // Act
        var result = _sut.Compare(first, second);

        // Assert
        result.EnqueueTimeDelta.Should().Be(TimeSpan.FromSeconds(5));
        result.FieldChanges.Select(static change => change.Field).Should().Contain([
            "Namespace",
            "Environment",
            "Entity",
            "Source",
            "MessageId",
            "CorrelationId",
            "SessionId",
            "ContentType",
            "Subject",
            "To",
            "ReplyTo",
            "ReplyToSessionId",
            "PartitionKey"
        ]);
        result.PropertyChanges.Should().Contain(change =>
            change.Key == "tenant" && change.Kind == MessagePropertyChangeKind.Modified);
        result.PropertyChanges.Should().Contain(change =>
            change.Key == "removed" && change.Kind == MessagePropertyChangeKind.Removed);
        result.PropertyChanges.Should().Contain(change =>
            change.Key == "added" && change.Kind == MessagePropertyChangeKind.Added);
        result.PropertyChanges.Should().Contain(change =>
            change.Key == "same" && change.Kind == MessagePropertyChangeKind.Unchanged);
        firstProperties.Should().HaveCount(3);
        secondProperties.Should().HaveCount(3);
    }

    private static CorrelationMessage CreateMessage(
        string messageId = "message-1",
        long sequenceNumber = 1,
        string body = "{}",
        DateTimeOffset? enqueuedTime = null,
        string namespaceName = "demo.servicebus.windows.net",
        ConnectionEnvironment environment = ConnectionEnvironment.Test,
        string entityName = "orders",
        CorrelationMessageSource source = CorrelationMessageSource.Loaded,
        string? correlationId = "corr-1",
        string? sessionId = "session-1",
        string? contentType = "application/json",
        string? subject = "created",
        string? to = null,
        string? replyTo = null,
        string? replyToSessionId = null,
        string? partitionKey = null,
        IReadOnlyDictionary<string, object>? properties = null) =>
        new(
            source,
            namespaceName,
            environment,
            entityName,
            "Queue",
            null,
            null,
            messageId,
            correlationId,
            sessionId,
            contentType,
            body,
            enqueuedTime ?? DateTimeOffset.Parse("2026-07-28T09:00:00Z"),
            sequenceNumber,
            properties ?? new Dictionary<string, object>(),
            subject,
            to,
            replyTo,
            replyToSessionId,
            partitionKey);
}
