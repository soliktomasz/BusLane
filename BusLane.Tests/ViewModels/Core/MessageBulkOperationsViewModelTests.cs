namespace BusLane.Tests.ViewModels.Core;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;

public class MessageBulkOperationsViewModelTests
{
    [Fact]
    public async Task GetPurgeConfirmationMessageAsync_WhenPreviewAvailable_FormatsScopeEstimateAndWarnings()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.PreviewPurgeMessagesAsync("orders", null, true, Arg.Any<CancellationToken>())
            .Returns(new BulkOperationPreview(
                BulkOperationType.Purge,
                "orders (DLQ)",
                42,
                [],
                ["Session-enabled entity", "Irreversible operation"]));

        var preferences = Substitute.For<IPreferencesService>();
        var logSink = Substitute.For<ILogSink>();
        var navigation = new NavigationState
        {
            SelectedQueue = CreateQueueInfo("orders"),
            ShowDeadLetter = true
        };

        var sut = new MessageBulkOperationsViewModel(
            () => operations,
            () => navigation,
            preferences,
            logSink,
            _ => { });

        // Act
        var message = await sut.GetPurgeConfirmationMessageAsync();

        // Assert
        message.Should().Contain("Purge scope: orders (DLQ)");
        message.Should().Contain("Estimated messages: 42");
        message.Should().Contain("Warnings:");
        message.Should().Contain("Session-enabled entity");
        message.Should().Contain("Irreversible operation");
    }

    [Fact]
    public void BuildBulkDeletePreview_WithSessionMessages_IncludesScopeWarnings()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var preferences = Substitute.For<IPreferencesService>();
        var logSink = Substitute.For<ILogSink>();
        var navigation = new NavigationState
        {
            SelectedQueue = CreateQueueInfo("orders", requiresSession: true),
            SelectedEntity = CreateQueueInfo("orders", requiresSession: true),
            ShowDeadLetter = true
        };

        var sut = new MessageBulkOperationsViewModel(
            () => operations,
            () => navigation,
            preferences,
            logSink,
            _ => { });

        var selectedMessages = new ObservableCollection<MessageInfo>
        {
            CreateMessage("msg-1", 101, "session-a"),
            CreateMessage("msg-2", 102, "session-a"),
            CreateMessage("msg-3", 103, "session-b")
        };

        // Act
        var preview = sut.BuildBulkDeletePreview(selectedMessages);

        // Assert
        preview.OperationType.Should().Be(BulkOperationType.Delete);
        preview.EstimatedImpactedCount.Should().Be(3);
        preview.ScopeDescription.Should().Contain("orders");
        preview.ScopeDescription.Should().Contain("DLQ");
        preview.RequiresSession.Should().BeTrue();
        preview.SampleMessageIds.Should().ContainInOrder("msg-1", "msg-2", "msg-3");
        preview.Warnings.Should().Contain(w => w.Contains("session", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConfirmationMessages_WithSelectedMessages_IncludeSampleIdsAndSelectionWarnings()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var preferences = Substitute.For<IPreferencesService>();
        var logSink = Substitute.For<ILogSink>();
        var navigation = new NavigationState
        {
            SelectedQueue = CreateQueueInfo("orders", requiresSession: true),
            SelectedEntity = CreateQueueInfo("orders", requiresSession: true),
            ShowDeadLetter = true
        };

        var sut = new MessageBulkOperationsViewModel(
            () => operations,
            () => navigation,
            preferences,
            logSink,
            _ => { });

        var selectedMessages = new ObservableCollection<MessageInfo>
        {
            CreateMessage("msg-1", 101, "session-a"),
            CreateMessage("msg-2", 102, "session-b")
        };

        // Act
        var resendMessage = sut.GetBulkResendConfirmationMessage(selectedMessages);
        var deleteMessage = sut.GetBulkDeleteConfirmationMessage(selectedMessages);
        var resubmitMessage = sut.GetResubmitDeadLettersConfirmationMessage(selectedMessages);

        // Assert
        resendMessage.Should().Contain("Estimated messages: 2");
        resendMessage.Should().Contain("Sample message IDs:");
        resendMessage.Should().Contain("msg-1");
        resendMessage.Should().Contain("msg-2");
        resendMessage.Should().Contain("session-enabled entity");

        deleteMessage.Should().Contain("Sample message IDs:");
        deleteMessage.Should().Contain("msg-1");
        deleteMessage.Should().Contain("Deleting from the dead-letter queue is irreversible.");

        resubmitMessage.Should().Contain("Sample message IDs:");
        resubmitMessage.Should().Contain("msg-1");
        resubmitMessage.Should().Contain("session-enabled entity");
    }

    [Fact]
    public async Task ExecuteBulkDeleteDetailedAsync_WhenOperationPartiallyFails_PreservesFailedMessageIdentifiers()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.DeleteMessagesDetailedAsync(
                "orders",
                null,
                Arg.Any<IEnumerable<MessageIdentifier>>(),
                false,
                Arg.Any<CancellationToken>())
            .Returns(new BulkOperationExecutionResult(
                BulkOperationType.Delete,
                RequestedCount: 2,
                SucceededCount: 1,
                FailedIdentifiers: [new MessageIdentifier(102, "msg-2")],
                Summary: "Deleted 1 of 2 message(s)",
                CanResume: true));

        var preferences = Substitute.For<IPreferencesService>();
        var logSink = Substitute.For<ILogSink>();
        var navigation = new NavigationState
        {
            SelectedQueue = CreateQueueInfo("orders"),
            SelectedEntity = CreateQueueInfo("orders")
        };

        var statuses = new List<string>();
        var sut = new MessageBulkOperationsViewModel(
            () => operations,
            () => navigation,
            preferences,
            logSink,
            statuses.Add);

        var selectedMessages = new ObservableCollection<MessageInfo>
        {
            CreateMessage("msg-1", 101),
            CreateMessage("msg-2", 102)
        };

        // Act
        var result = await sut.ExecuteBulkDeleteDetailedAsync(selectedMessages);

        // Assert
        result.FailedCount.Should().Be(1);
        result.FailedIdentifiers.Should().ContainSingle(i => i.MessageId == "msg-2" && i.SequenceNumber == 102);
        result.CanResume.Should().BeTrue();
        statuses.Should().Contain(s => s.Contains("Deleted 1 of 2", StringComparison.OrdinalIgnoreCase));
    }

    private static QueueInfo CreateQueueInfo(string name, bool requiresSession = false)
    {
        return new QueueInfo(
            name,
            MessageCount: 10,
            ActiveMessageCount: 7,
            DeadLetterCount: 3,
            ScheduledCount: 0,
            SizeInBytes: 1024,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: requiresSession,
            DefaultMessageTtl: TimeSpan.FromDays(14),
            LockDuration: TimeSpan.FromMinutes(1));
    }

    private static MessageInfo CreateMessage(string messageId, long sequenceNumber, string? sessionId = null)
    {
        return new MessageInfo(
            messageId,
            CorrelationId: null,
            ContentType: "application/json",
            Body: "{}",
            EnqueuedTime: DateTimeOffset.UtcNow,
            ScheduledEnqueueTime: null,
            SequenceNumber: sequenceNumber,
            DeliveryCount: 1,
            SessionId: sessionId,
            Properties: new Dictionary<string, object>());
    }
}
