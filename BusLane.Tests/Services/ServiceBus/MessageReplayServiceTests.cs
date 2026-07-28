namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;

public class MessageReplayServiceTests
{
    [Fact]
    public void CreateRequest_PreservesEditableDataAndGeneratesNewMessageId()
    {
        // Arrange
        var sut = CreateSut();
        var source = CreateSource();
        var destination = CreateDestination();

        // Act
        var request = sut.CreateRequest(source, destination);

        // Assert
        request.Body.Should().Be(source.Body);
        request.CorrelationId.Should().Be(source.CorrelationId);
        request.SessionId.Should().Be(source.SessionId);
        request.Subject.Should().Be(source.Subject);
        request.Properties.Should().BeEquivalentTo(source.Properties);
        request.MessageId.Should().NotBeNullOrWhiteSpace().And.NotBe(source.MessageId);
    }

    [Fact]
    public void Preview_WithInvalidFields_ReturnsAllValidationErrors()
    {
        // Arrange
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-28T10:00:00Z"));
        var sut = CreateSut(time);
        var request = sut.CreateRequest(CreateSource() with { SessionId = null }, CreateDestination() with
        {
            EntityName = "",
            RequiresSession = true
        }) with
        {
            Body = "",
            RateLimitPerSecond = 0,
            ScheduledEnqueueTime = time.GetUtcNow().AddMinutes(-1),
            Properties = new Dictionary<string, object> { [""] = "invalid" }
        };

        // Act
        var preview = sut.Preview(request);

        // Assert
        preview.IsValid.Should().BeFalse();
        preview.ValidationErrors.Should().Contain([
            "Destination entity is required",
            "Message body is required",
            "Rate limit must be greater than zero",
            "Scheduled enqueue time must be in the future",
            "Session ID is required for the selected destination",
            "Application property keys cannot be empty"
        ]);
    }

    [Fact]
    public async Task ReplayAsync_WithoutGeneralConfirmation_DoesNotSend()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var sut = CreateSut();
        var request = sut.CreateRequest(CreateSource(), CreateDestination());

        // Act
        var result = await sut.ReplayAsync(operations, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Replay confirmation is required");
        await operations.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
    }

    [Fact]
    public async Task ReplayAsync_ToProductionWithoutAcknowledgement_DoesNotSend()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var sut = CreateSut();
        var request = sut.CreateRequest(
            CreateSource(),
            CreateDestination() with { Environment = ConnectionEnvironment.Production }) with
        {
            IsConfirmed = true
        };

        // Act
        var result = await sut.ReplayAsync(operations, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Production replay acknowledgement is required");
        await operations.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
    }

    [Fact]
    public async Task ReplayAsync_WithImmediateRequest_SendsEditedMessage()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var sut = CreateSut();
        var request = sut.CreateRequest(CreateSource(), CreateDestination()) with
        {
            MessageId = "deliberate-message-id",
            Body = "{\"edited\":true}",
            IsConfirmed = true
        };

        // Act
        var result = await sut.ReplayAsync(operations, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsScheduled.Should().BeFalse();
        await operations.Received(1).SendMessageAsync(
            "orders-replay",
            "{\"edited\":true}",
            Arg.Is<IDictionary<string, object>>(properties =>
                properties != null && properties["tenant"].Equals("north")),
            "application/json",
            "corr-1",
            "deliberate-message-id",
            "session-1",
            "created",
            "processor",
            "replies",
            "reply-session",
            "partition-1",
            TimeSpan.FromMinutes(10),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayAsync_WithScheduledRequest_UsesScheduleApi()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.ScheduleMessageAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(99);
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-28T10:00:00Z"));
        var sut = CreateSut(time);
        var scheduledAt = time.GetUtcNow().AddHours(1);
        var request = sut.CreateRequest(CreateSource(), CreateDestination()) with
        {
            ScheduledEnqueueTime = scheduledAt,
            IsConfirmed = true
        };

        // Act
        var result = await sut.ReplayAsync(operations, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsScheduled.Should().BeTrue();
        result.ScheduledSequenceNumber.Should().Be(99);
        await operations.Received(1).ScheduleMessageAsync(
            "orders-replay",
            request.Body,
            Arg.Any<IDictionary<string, object>>(),
            scheduledAt,
            request.ContentType,
            request.CorrelationId,
            request.MessageId,
            request.SessionId,
            request.Subject,
            request.To,
            request.ReplyTo,
            request.ReplyToSessionId,
            request.PartitionKey,
            request.TimeToLive,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayAsync_WhenAttemptsAreFasterThanLimit_DelaysSecondAttempt()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var delay = Substitute.For<IReplayDelay>();
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-28T10:00:00Z"));
        var sut = CreateSut(time, delay);
        var request = sut.CreateRequest(CreateSource(), CreateDestination()) with
        {
            RateLimitPerSecond = 2,
            IsConfirmed = true
        };
        await sut.ReplayAsync(operations, request);
        time.Advance(TimeSpan.FromMilliseconds(100));

        // Act
        await sut.ReplayAsync(operations, request);

        // Assert
        await delay.Received(1).DelayAsync(TimeSpan.FromMilliseconds(400), Arg.Any<CancellationToken>());
    }

    private static MessageReplayService CreateSut(
        TimeProvider? timeProvider = null,
        IReplayDelay? delay = null)
    {
        return new MessageReplayService(
            timeProvider ?? new ManualTimeProvider(DateTimeOffset.Parse("2026-07-28T10:00:00Z")),
            delay ?? Substitute.For<IReplayDelay>());
    }

    private static ReplayDestination CreateDestination()
    {
        return new ReplayDestination(
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders-replay",
            "Queue",
            RequiresSession: false);
    }

    private static CorrelationMessage CreateSource()
    {
        return new CorrelationMessage(
            CorrelationMessageSource.Loaded,
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            "message-1",
            "corr-1",
            "session-1",
            "application/json",
            "{}",
            DateTimeOffset.Parse("2026-07-28T09:00:00Z"),
            42,
            new Dictionary<string, object> { ["tenant"] = "north" },
            "created",
            "processor",
            "replies",
            "reply-session",
            "partition-1",
            TimeSpan.FromMinutes(10));
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
