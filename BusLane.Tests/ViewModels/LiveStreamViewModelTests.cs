namespace BusLane.Tests.ViewModels;

using System.Collections.Specialized;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class LiveStreamViewModelTests
{
    [Fact]
    public async Task IncomingMessage_AfterFlush_AddsMessageToCorrelationCatalog()
    {
        // Arrange
        var liveStreamService = new FakeLiveStreamService();
        var catalog = Substitute.For<ICorrelationMessageCatalog>();
        await using var sut = new LiveStreamViewModel(
            liveStreamService,
            () => null,
            catalog,
            () => new CorrelationSourceContext(
                "demo.servicebus.windows.net",
                ConnectionEnvironment.Test,
                "",
                "",
                null,
                null));

        // Act
        liveStreamService.EmitMessage(CreateMessage("message-1", 1) with { CorrelationId = "corr-1" });
        await WaitForAsync(() => sut.Messages.Count == 1);

        // Assert
        catalog.Received(1).Add(Arg.Is<CorrelationMessage>(message =>
            message != null &&
            message.MessageId == "message-1" &&
            message.Source == CorrelationMessageSource.LiveStream));
    }

    [Fact]
    public async Task IncomingMessages_AfterInitialFlush_DoesNotResetMessagesCollection()
    {
        // Arrange
        var liveStreamService = new FakeLiveStreamService();
        await using var sut = new LiveStreamViewModel(liveStreamService, () => null);

        liveStreamService.EmitMessage(CreateMessage("message-1", 1));
        liveStreamService.EmitMessage(CreateMessage("message-2", 2));
        await WaitForAsync(() => sut.Messages.Count == 2);

        var actions = new List<NotifyCollectionChangedAction>();
        sut.Messages.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        liveStreamService.EmitMessage(CreateMessage("message-3", 3));
        await WaitForAsync(() => sut.Messages.Count == 3);

        // Assert
        actions.Should().NotContain(NotifyCollectionChangedAction.Reset);
        sut.Messages.Select(message => message.MessageId)
            .Should().ContainInOrder("message-3", "message-2", "message-1");
    }

    [Fact]
    public async Task ClearMessages_WithSelectedMessage_ClearsDetailSelection()
    {
        // Arrange
        var liveStreamService = new FakeLiveStreamService();
        await using var sut = new LiveStreamViewModel(liveStreamService, () => null);
        liveStreamService.EmitMessage(CreateMessage("message-1", 1));
        await WaitForAsync(() => sut.Messages.Count == 1);
        sut.SelectedMessage = sut.Messages.Single();

        // Act
        sut.ClearMessagesCommand.Execute(null);

        // Assert
        sut.SelectedMessage.Should().BeNull();
    }

    [Fact]
    public async Task FilterText_WhenSelectedMessageNoLongerMatches_ClearsDetailSelection()
    {
        // Arrange
        var liveStreamService = new FakeLiveStreamService();
        await using var sut = new LiveStreamViewModel(liveStreamService, () => null);
        liveStreamService.EmitMessage(CreateMessage("selected-message", 1));
        liveStreamService.EmitMessage(CreateMessage("visible-message", 2));
        await WaitForAsync(() => sut.Messages.Count == 2);
        sut.SelectedMessage = sut.Messages.Single(message => message.MessageId == "selected-message");

        // Act
        sut.FilterText = "visible-message";

        // Assert
        sut.SelectedMessage.Should().BeNull();
        sut.FilteredMessages.Should().ContainSingle(message => message.MessageId == "visible-message");
    }

    [Fact]
    public async Task StopStream_WithSelectedMessage_ClearsDetailSelection()
    {
        // Arrange
        var liveStreamService = new FakeLiveStreamService();
        await using var sut = new LiveStreamViewModel(liveStreamService, () => null);
        liveStreamService.EmitMessage(CreateMessage("message-1", 1));
        await WaitForAsync(() => sut.Messages.Count == 1);
        sut.SelectedMessage = sut.Messages.Single();

        // Act
        await sut.StopStreamCommand.ExecuteAsync(null);

        // Assert
        sut.SelectedMessage.Should().BeNull();
    }

    [Fact]
    public async Task IncomingMessage_WhenAutoScrollEnabled_SelectsNewestVisibleMessage()
    {
        // Arrange
        var liveStreamService = new FakeLiveStreamService();
        await using var sut = new LiveStreamViewModel(liveStreamService, () => null);
        liveStreamService.EmitMessage(CreateMessage("message-1", 1));
        await WaitForAsync(() => sut.Messages.Count == 1);

        // Act
        liveStreamService.EmitMessage(CreateMessage("message-2", 2));
        await WaitForAsync(() => sut.Messages.Count == 2);

        // Assert
        sut.SelectedMessage.Should().BeSameAs(sut.FilteredMessages[0]);
        sut.SelectedMessage!.MessageId.Should().Be("message-2");
    }

    private static LiveStreamMessage CreateMessage(string messageId, long sequenceNumber)
    {
        return new LiveStreamMessage(
            messageId,
            CorrelationId: null,
            ContentType: "application/json",
            Body: $"{{\"id\":\"{messageId}\"}}",
            EnqueuedAt: DateTimeOffset.UtcNow,
            EntityName: "orders",
            EntityType: "Queue",
            TopicName: null,
            SequenceNumber: sequenceNumber,
            SessionId: null,
            Properties: new Dictionary<string, object>());
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Condition was not met within the expected time.");
    }

    private sealed class FakeLiveStreamService : ILiveStreamService
    {
        private readonly Subject<LiveStreamMessage> _subject = new();

        public IObservable<LiveStreamMessage> Messages => _subject.AsObservable();
        public bool IsStreaming { get; private set; }

        public event EventHandler<bool>? StreamingStatusChanged;
        public event EventHandler<Exception>? StreamError
        {
            add { }
            remove { }
        }

        public Task StartQueueStreamAsync(IServiceBusOperations operations, string queueName, CancellationToken ct = default)
        {
            _ = operations;
            _ = queueName;
            ct.ThrowIfCancellationRequested();
            IsStreaming = true;
            StreamingStatusChanged?.Invoke(this, true);
            return Task.CompletedTask;
        }

        public Task StartSubscriptionStreamAsync(IServiceBusOperations operations, string topicName, string subscriptionName, CancellationToken ct = default)
        {
            _ = operations;
            _ = topicName;
            _ = subscriptionName;
            ct.ThrowIfCancellationRequested();
            IsStreaming = true;
            StreamingStatusChanged?.Invoke(this, true);
            return Task.CompletedTask;
        }

        public Task StopStreamAsync()
        {
            IsStreaming = false;
            StreamingStatusChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        public void EmitMessage(LiveStreamMessage message) => _subject.OnNext(message);

        public ValueTask DisposeAsync()
        {
            _subject.OnCompleted();
            _subject.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
