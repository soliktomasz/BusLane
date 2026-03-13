namespace BusLane.Tests.ViewModels;

using System.Collections.Specialized;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using FluentAssertions;

public class LiveStreamViewModelTests
{
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

    private static LiveStreamMessage CreateMessage(string messageId, long sequenceNumber)
    {
        return new LiveStreamMessage(
            messageId,
            CorrelationId: null,
            ContentType: "application/json",
            Body: $"{{\"id\":\"{messageId}\"}}",
            ReceivedAt: DateTimeOffset.UtcNow,
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

        public Task StartQueueStreamAsync(IServiceBusOperations operations, string queueName, bool peekOnly = true, CancellationToken ct = default)
        {
            _ = operations;
            _ = queueName;
            _ = peekOnly;
            ct.ThrowIfCancellationRequested();
            IsStreaming = true;
            StreamingStatusChanged?.Invoke(this, true);
            return Task.CompletedTask;
        }

        public Task StartSubscriptionStreamAsync(IServiceBusOperations operations, string topicName, string subscriptionName, bool peekOnly = true, CancellationToken ct = default)
        {
            _ = operations;
            _ = topicName;
            _ = subscriptionName;
            _ = peekOnly;
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
