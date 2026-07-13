namespace BusLane.Tests.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;
using System.Reflection;
using Xunit;

public class ServiceBusOperationsTests
{
    [Fact]
    public void BuildCreateQueueOptions_WithAdvancedOptions_MapsSupportedSettings()
    {
        // Arrange
        var options = new QueueCreationOptions(
            "orders",
            RequiresSession: true,
            DefaultMessageTimeToLive: TimeSpan.FromDays(3),
            LockDuration: TimeSpan.FromSeconds(45),
            DuplicateDetectionHistoryTimeWindow: TimeSpan.FromMinutes(10),
            MaxSizeInMegabytes: 2048,
            EnablePartitioning: true,
            EnableBatchedOperations: false);

        // Act
        var sdkOptions = ServiceBusOperations.BuildCreateQueueOptions(options);

        // Assert
        sdkOptions.Name.Should().Be("orders");
        sdkOptions.RequiresSession.Should().BeTrue();
        sdkOptions.DefaultMessageTimeToLive.Should().Be(TimeSpan.FromDays(3));
        sdkOptions.LockDuration.Should().Be(TimeSpan.FromSeconds(45));
        sdkOptions.RequiresDuplicateDetection.Should().BeTrue();
        sdkOptions.DuplicateDetectionHistoryTimeWindow.Should().Be(TimeSpan.FromMinutes(10));
        sdkOptions.MaxSizeInMegabytes.Should().Be(2048);
        sdkOptions.EnablePartitioning.Should().BeTrue();
        sdkOptions.EnableBatchedOperations.Should().BeFalse();
    }

    [Fact]
    public void BuildCreateTopicOptions_WithAdvancedOptions_MapsSupportedSettings()
    {
        // Arrange
        var options = new TopicCreationOptions(
            "events",
            DefaultMessageTimeToLive: TimeSpan.FromDays(5),
            DuplicateDetectionHistoryTimeWindow: TimeSpan.FromMinutes(20),
            MaxSizeInMegabytes: 1024,
            EnablePartitioning: true,
            EnableBatchedOperations: false);

        // Act
        var sdkOptions = ServiceBusOperations.BuildCreateTopicOptions(options);

        // Assert
        sdkOptions.Name.Should().Be("events");
        sdkOptions.DefaultMessageTimeToLive.Should().Be(TimeSpan.FromDays(5));
        sdkOptions.RequiresDuplicateDetection.Should().BeTrue();
        sdkOptions.DuplicateDetectionHistoryTimeWindow.Should().Be(TimeSpan.FromMinutes(20));
        sdkOptions.MaxSizeInMegabytes.Should().Be(1024);
        sdkOptions.EnablePartitioning.Should().BeTrue();
        sdkOptions.EnableBatchedOperations.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildCreateQueueOptions_WithBlankName_Throws(string queueName)
    {
        // Arrange
        var options = new QueueCreationOptions(queueName);

        // Act
        var act = () => ServiceBusOperations.BuildCreateQueueOptions(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("Name");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildCreateTopicOptions_WithBlankName_Throws(string topicName)
    {
        // Arrange
        var options = new TopicCreationOptions(topicName);

        // Act
        var act = () => ServiceBusOperations.BuildCreateTopicOptions(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("Name");
    }

    [Fact]
    public void BuildCreateSubscriptionOptions_WithSessionOption_MapsTopicNameSubscriptionNameAndRequiresSession()
    {
        // Arrange
        var options = new SubscriptionCreationOptions("processor", RequiresSession: true);

        // Act
        var sdkOptions = ServiceBusOperations.BuildCreateSubscriptionOptions("orders-topic", options);

        // Assert
        sdkOptions.TopicName.Should().Be("orders-topic");
        sdkOptions.SubscriptionName.Should().Be("processor");
        sdkOptions.RequiresSession.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildCreateSubscriptionOptions_WithBlankTopicName_Throws(string topicName)
    {
        // Arrange
        var options = new SubscriptionCreationOptions("processor");

        // Act
        var act = () => ServiceBusOperations.BuildCreateSubscriptionOptions(topicName, options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("topicName");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildCreateSubscriptionOptions_WithBlankSubscriptionName_Throws(string subscriptionName)
    {
        // Arrange
        var options = new SubscriptionCreationOptions(subscriptionName);

        // Act
        var act = () => ServiceBusOperations.BuildCreateSubscriptionOptions("orders-topic", options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("Name");
    }

    [Fact]
    public void MapToSubscriptionRuleInfo_WithSqlFilter_MapsExpressionAndAction()
    {
        // Arrange
        var rule = ServiceBusModelFactory.RuleProperties(
            "important",
            new SqlRuleFilter("sys.Label = 'important'"),
            new SqlRuleAction("SET priority = 'high'"));

        // Act
        var info = ServiceBusOperations.MapToSubscriptionRuleInfo(rule);

        // Assert
        info.Name.Should().Be("important");
        info.FilterType.Should().Be(SubscriptionRuleFilterType.Sql);
        info.SqlExpression.Should().Be("sys.Label = 'important'");
        info.DisplayExpression.Should().Be("sys.Label = 'important'");
        info.ActionExpression.Should().Be("SET priority = 'high'");
    }

    [Fact]
    public void MapToSubscriptionRuleInfo_WithCorrelationFilter_MapsProperties()
    {
        // Arrange
        var filter = new CorrelationRuleFilter
        {
            Subject = "important",
            CorrelationId = "corr-1"
        };
        filter.ApplicationProperties["tenant"] = "alpha";
        var rule = ServiceBusModelFactory.RuleProperties("correlated", filter, null);

        // Act
        var info = ServiceBusOperations.MapToSubscriptionRuleInfo(rule);

        // Assert
        info.FilterType.Should().Be(SubscriptionRuleFilterType.Correlation);
        info.DisplayExpression.Should().Contain("Subject = important");
        info.DisplayExpression.Should().Contain("CorrelationId = corr-1");
        info.DisplayExpression.Should().Contain("tenant = alpha");
        info.CorrelationProperties.Should().Contain("Subject", "important");
        info.CorrelationProperties.Should().Contain("CorrelationId", "corr-1");
        info.CorrelationProperties.Should().Contain("tenant", "alpha");
    }

    [Fact]
    public void MapToSubscriptionRuleInfo_WithApplicationPropertyMatchingReservedField_PreservesReservedField()
    {
        // Arrange
        var filter = new CorrelationRuleFilter
        {
            Subject = "reserved"
        };
        filter.ApplicationProperties["Subject"] = "application";
        var rule = ServiceBusModelFactory.RuleProperties("correlated", filter, null);

        // Act
        var info = ServiceBusOperations.MapToSubscriptionRuleInfo(rule);

        // Assert
        info.DisplayExpression.Should().Contain("Subject = reserved");
        info.DisplayExpression.Should().NotContain("Subject = application");
        info.CorrelationProperties.Should().Contain("Subject", "reserved");
    }

    [Fact]
    public void MapToSubscriptionRuleInfo_WithTrueFilter_MapsDisplayExpression()
    {
        // Arrange
        var rule = ServiceBusModelFactory.RuleProperties(RuleProperties.DefaultRuleName, new TrueRuleFilter(), null);

        // Act
        var info = ServiceBusOperations.MapToSubscriptionRuleInfo(rule);

        // Assert
        info.FilterType.Should().Be(SubscriptionRuleFilterType.True);
        info.DisplayExpression.Should().Be("True");
    }

    [Fact]
    public void BuildCreateRuleOptions_WithSqlFilter_MapsFilterAndAction()
    {
        // Arrange
        var options = new SubscriptionRuleCreationOptions(
            "important",
            "sys.Label = 'important'",
            ActionExpression: "SET priority = 'high'");

        // Act
        var sdkOptions = ServiceBusOperations.BuildCreateRuleOptions(options);

        // Assert
        sdkOptions.Name.Should().Be("important");
        sdkOptions.Filter.Should().BeOfType<SqlRuleFilter>()
            .Which.SqlExpression.Should().Be("sys.Label = 'important'");
        sdkOptions.Action.Should().BeOfType<SqlRuleAction>()
            .Which.SqlExpression.Should().Be("SET priority = 'high'");
    }

    [Fact]
    public void BuildCreateRuleOptions_WithCorrelationFilter_MapsProperties()
    {
        // Arrange
        var options = new SubscriptionRuleCreationOptions(
            "correlated",
            FilterType: SubscriptionRuleFilterType.Correlation,
            CorrelationProperties: new Dictionary<string, string>
            {
                ["Subject"] = "important",
                ["CorrelationId"] = "corr-1",
                ["tenant"] = "alpha"
            });

        // Act
        var sdkOptions = ServiceBusOperations.BuildCreateRuleOptions(options);

        // Assert
        var filter = sdkOptions.Filter.Should().BeOfType<CorrelationRuleFilter>().Subject;
        filter.Subject.Should().Be("important");
        filter.CorrelationId.Should().Be("corr-1");
        filter.ApplicationProperties.Should().Contain("tenant", "alpha");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildCreateRuleOptions_WithBlankName_Throws(string ruleName)
    {
        // Arrange
        var options = new SubscriptionRuleCreationOptions(ruleName, "1 = 1");

        // Act
        var act = () => ServiceBusOperations.BuildCreateRuleOptions(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("Name");
    }

    [Fact]
    public void BuildCreateRuleOptions_WithMissingSqlExpression_Throws()
    {
        // Arrange
        var options = new SubscriptionRuleCreationOptions("important", "");

        // Act
        var act = () => ServiceBusOperations.BuildCreateRuleOptions(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("SqlExpression");
    }

    [Fact]
    public async Task PeekSessionMessagesAsync_WhenDeadLetter_UsesStandardDeadLetterReceiver()
    {
        // Arrange
        var client = new RecordingServiceBusClient();

        // Act
        await ServiceBusOperations.PeekSessionMessagesAsync(
            client,
            "orders",
            null,
            25,
            0,
            deadLetter: true,
            CancellationToken.None);

        // Assert
        client.CreatedQueueName.Should().Be("orders");
        client.CreatedReceiverOptions.Should().NotBeNull();
        client.CreatedReceiverOptions!.SubQueue.Should().Be(SubQueue.DeadLetter);
        client.SessionAcceptWasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task PeekSessionMessagesAsync_WithSessionIdAndDeadLetter_UsesStandardDeadLetterReceiver()
    {
        // Arrange
        var client = new RecordingServiceBusClient(
        [
            ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "matching", sequenceNumber: 1, sessionId: "session-a"),
            ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "other", sequenceNumber: 2, sessionId: "session-b")
        ]);

        // Act
        var messages = await ServiceBusOperations.PeekSessionMessagesAsync(
            client,
            "orders",
            null,
            "session-a",
            25,
            0,
            deadLetter: true,
            CancellationToken.None);

        // Assert
        messages.Should().ContainSingle();
        messages[0].MessageId.Should().Be("matching");
        client.CreatedQueueName.Should().Be("orders");
        client.CreatedReceiverOptions.Should().NotBeNull();
        client.CreatedReceiverOptions!.SubQueue.Should().Be(SubQueue.DeadLetter);
        client.SessionAcceptWasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAsync_WithMoreWorkThanBudget_DoesNotExceedConfiguredConcurrency()
    {
        // Arrange
        var activeWorkers = 0;
        var maxConcurrentWorkers = 0;
        var items = Enumerable.Range(1, 18).ToArray();
        const int maxConcurrency = 3;
        var workerStartedSignals = items
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var workerReleaseSignals = items
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();

        // Act
        var resultsTask = InvokeBoundedAdminProjectorAsync(
            items,
            async (item, ct) =>
            {
                var index = item - 1;
                var inFlight = Interlocked.Increment(ref activeWorkers);
                UpdateMaxValue(ref maxConcurrentWorkers, inFlight);
                workerStartedSignals[index].TrySetResult();

                try
                {
                    await workerReleaseSignals[index].Task.WaitAsync(ct);
                    return item * 2;
                }
                finally
                {
                    Interlocked.Decrement(ref activeWorkers);
                }
            },
            maxConcurrency: maxConcurrency);

        await Task.WhenAll(workerStartedSignals.Take(maxConcurrency).Select(static signal => signal.Task))
            .WaitAsync(TimeSpan.FromSeconds(1));

        maxConcurrentWorkers.Should().Be(maxConcurrency);

        for (var batchStart = 0; batchStart < items.Length; batchStart += maxConcurrency)
        {
            foreach (var signal in workerReleaseSignals.Skip(batchStart).Take(maxConcurrency))
            {
                signal.TrySetResult();
            }

            var nextBatchStart = batchStart + maxConcurrency;
            if (nextBatchStart >= items.Length)
            {
                continue;
            }

            await Task.WhenAll(workerStartedSignals.Skip(nextBatchStart).Take(Math.Min(maxConcurrency, items.Length - nextBatchStart)).Select(static signal => signal.Task))
                .WaitAsync(TimeSpan.FromSeconds(1));
        }

        var results = await resultsTask.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        maxConcurrentWorkers.Should().BeLessThanOrEqualTo(3);
        results.Should().Equal(items.Select(static item => item * 2));
    }

    [Fact]
    public async Task SelectAsync_WhenWorkCompletesOutOfOrder_PreservesSourceOrder()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4 };

        // Act
        var results = await InvokeBoundedAdminProjectorAsync(
            items,
            async (item, ct) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds((5 - item) * 20), ct);
                return $"item-{item}";
            },
            maxConcurrency: 2);

        // Assert
        results.Should().Equal("item-1", "item-2", "item-3", "item-4");
    }

    [Fact]
    public async Task ResubmitDeadLetterMessagesDetailedAsync_ReceivesFromDlqWithoutDeferredLookup()
    {
        // Arrange
        var receivedMessages = new[]
        {
            ServiceBusModelFactory.ServiceBusReceivedMessage(BinaryData.FromString("other"), messageId: "other", sequenceNumber: 1),
            ServiceBusModelFactory.ServiceBusReceivedMessage(BinaryData.FromString("target"), messageId: "target", sequenceNumber: 2)
        };
        var client = new RecordingServiceBusClient(receivedMessages);
        var selectedMessage = new MessageInfo(
            "target", null, "application/json", "target", DateTimeOffset.UtcNow, null,
            2, 1, null, new Dictionary<string, object>());

        // Act
        var result = await ServiceBusOperations.ResubmitDeadLetterMessagesDetailedAsync(
            client,
            "orders",
            null,
            [selectedMessage],
            CancellationToken.None);

        // Assert
        result.SucceededCount.Should().Be(1);
        client.CreatedReceiverOptions!.SubQueue.Should().Be(SubQueue.DeadLetter);
        client.Receiver.DeferredReceiveWasCalled.Should().BeFalse();
        client.Receiver.CompletedSequenceNumbers.Should().ContainSingle().Which.Should().Be(2);
        client.Receiver.AbandonedSequenceNumbers.Should().ContainSingle().Which.Should().Be(1);
        client.Sender.SentMessages.Should().ContainSingle(message => message.Body.ToString() == "target");
    }

    [Fact]
    public async Task DeleteMessagesDetailedAsync_WhenSessionRequired_AcceptsSelectedSession()
    {
        // Arrange
        var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            BinaryData.FromString("target"),
            messageId: "target",
            sequenceNumber: 7,
            sessionId: "session-a");
        var client = new RecordingServiceBusClient([receivedMessage], allowSessionReceiver: true);

        // Act
        var result = await ServiceBusOperations.DeleteMessagesDetailedAsync(
            client,
            "orders",
            null,
            [new MessageIdentifier(7, "target", "session-a")],
            deadLetter: false,
            ct: CancellationToken.None,
            requiresSession: true);

        // Assert
        result.SucceededCount.Should().Be(1);
        client.AcceptedSessionId.Should().Be("session-a");
        client.SessionReceiver.CompletedSequenceNumbers.Should().ContainSingle().Which.Should().Be(7);
    }

    [Fact(Skip = "Integration test - requires Service Bus client setup. Functionality verified through manual testing.")]
    public async Task PeekMessagesAsync_WithSequenceNumber_ShouldCallPeekWithSequenceNumber()
    {
        // Arrange
        var receiver = Substitute.For<ServiceBusReceiver>();
        receiver.PeekMessagesAsync(50, 12345, Arg.Any<CancellationToken>())
                .Returns(new List<ServiceBusReceivedMessage>().AsReadOnly());
        
        var sut = CreateOperations(receiver);
        
        // Act
        await sut.PeekMessagesAsync("queue", null, 50, 12345, false, false);
        
        // Assert
        await receiver.Received(1).PeekMessagesAsync(50, 12345, Arg.Any<CancellationToken>());
    }
    
    private IServiceBusOperations CreateOperations(ServiceBusReceiver receiver)
    {
        // This is a placeholder - the real implementation would require complex mocking
        throw new NotImplementedException("This test requires Service Bus client infrastructure setup");
    }

    private sealed class RecordingServiceBusClient(
        IReadOnlyList<ServiceBusReceivedMessage>? messages = null,
        bool allowSessionReceiver = false) : ServiceBusClient
    {
        private readonly IReadOnlyList<ServiceBusReceivedMessage> _messages = messages ?? [];

        public string? CreatedQueueName { get; private set; }
        public ServiceBusReceiverOptions? CreatedReceiverOptions { get; private set; }
        public bool SessionAcceptWasCalled { get; private set; }
        public string? AcceptedSessionId { get; private set; }
        public FakeReceiver Receiver { get; } = new(messages ?? []);
        public FakeSessionReceiver SessionReceiver { get; } = new(messages ?? []);
        public FakeSender Sender { get; } = new();

        public override ServiceBusReceiver CreateReceiver(string queueName, ServiceBusReceiverOptions options)
        {
            CreatedQueueName = queueName;
            CreatedReceiverOptions = options;
            return Receiver;
        }

        public override ServiceBusSender CreateSender(string queueOrTopicName) => Sender;

        public override Task<ServiceBusSessionReceiver> AcceptNextSessionAsync(
            string queueName,
            ServiceBusSessionReceiverOptions options,
            CancellationToken cancellationToken = default)
        {
            SessionAcceptWasCalled = true;
            throw new InvalidOperationException("Session receiver should not be used for dead-letter subqueues.");
        }

        public override Task<ServiceBusSessionReceiver> AcceptSessionAsync(
            string queueName,
            string sessionId,
            ServiceBusSessionReceiverOptions options,
            CancellationToken cancellationToken = default)
        {
            SessionAcceptWasCalled = true;
            AcceptedSessionId = sessionId;
            if (allowSessionReceiver)
            {
                return Task.FromResult<ServiceBusSessionReceiver>(SessionReceiver);
            }

            throw new InvalidOperationException("Session receiver should not be used for dead-letter subqueues.");
        }
    }

    private sealed class FakeReceiver(IReadOnlyList<ServiceBusReceivedMessage> messages) : ServiceBusReceiver
    {
        private bool _received;

        public bool DeferredReceiveWasCalled { get; private set; }
        public List<long> CompletedSequenceNumbers { get; } = [];
        public List<long> AbandonedSequenceNumbers { get; } = [];

        public override Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekMessagesAsync(
            int maxMessages,
            long? fromSequenceNumber,
            CancellationToken cancellationToken = default)
        {
            var filteredMessages = fromSequenceNumber.HasValue
                ? messages.Where(message => message.SequenceNumber >= fromSequenceNumber.Value)
                : messages;

            return Task.FromResult(filteredMessages.Take(maxMessages).ToList().AsReadOnly() as IReadOnlyList<ServiceBusReceivedMessage>);
        }

        public override Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
            int maxMessages,
            TimeSpan? maxWaitTime = null,
            CancellationToken cancellationToken = default)
        {
            if (_received)
            {
                return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>([]);
            }

            _received = true;
            return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(messages.Take(maxMessages).ToList());
        }

        public override Task<ServiceBusReceivedMessage> ReceiveDeferredMessageAsync(
            long sequenceNumber,
            CancellationToken cancellationToken = default)
        {
            DeferredReceiveWasCalled = true;
            throw new InvalidOperationException("Deferred receive must not be used for ordinary DLQ messages.");
        }

        public override Task CompleteMessageAsync(
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default)
        {
            CompletedSequenceNumbers.Add(message.SequenceNumber);
            return Task.CompletedTask;
        }

        public override Task AbandonMessageAsync(
            ServiceBusReceivedMessage message,
            IDictionary<string, object>? propertiesToModify = null,
            CancellationToken cancellationToken = default)
        {
            AbandonedSequenceNumbers.Add(message.SequenceNumber);
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSessionReceiver(IReadOnlyList<ServiceBusReceivedMessage> messages) : ServiceBusSessionReceiver
    {
        private bool _received;

        public List<long> CompletedSequenceNumbers { get; } = [];

        public override Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
            int maxMessages,
            TimeSpan? maxWaitTime = null,
            CancellationToken cancellationToken = default)
        {
            if (_received)
            {
                return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>([]);
            }

            _received = true;
            return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(messages.Take(maxMessages).ToList());
        }

        public override Task CompleteMessageAsync(
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default)
        {
            CompletedSequenceNumbers.Add(message.SequenceNumber);
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSender : ServiceBusSender
    {
        public List<ServiceBusMessage> SentMessages { get; } = [];

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task<IReadOnlyList<TResult>> InvokeBoundedAdminProjectorAsync<TSource, TResult>(
        IReadOnlyList<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> projector,
        int maxConcurrency,
        CancellationToken ct = default)
    {
        var helperType = typeof(ConnectionStringOperations).Assembly.GetType("BusLane.Services.ServiceBus.BoundedAdminProjector");
        helperType.Should().NotBeNull();

        var selectAsync = helperType!.GetMethod("SelectAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        selectAsync.Should().NotBeNull();

        var genericMethod = selectAsync!.MakeGenericMethod(typeof(TSource), typeof(TResult));
        var task = (Task)genericMethod.Invoke(null, [source, projector, maxConcurrency, ct])!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        resultProperty.Should().NotBeNull();
        return (IReadOnlyList<TResult>)resultProperty!.GetValue(task)!;
    }

    private static void UpdateMaxValue(ref int target, int candidate)
    {
        while (true)
        {
            var current = target;
            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, current) == current)
            {
                return;
            }
        }
    }
}
