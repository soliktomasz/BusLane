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
    public async Task ShouldConfirmBulkOperations_WhenPreferencesDisabled_StillRequiresPreviewConfirmation()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var preferences = Substitute.For<IPreferencesService>();
        preferences.ConfirmBeforePurge.Returns(false);
        var logSink = Substitute.For<ILogSink>();
        var navigation = new NavigationState
        {
            SelectedQueue = CreateQueueInfo("orders")
        };

        var sut = new MessageBulkOperationsViewModel(
            () => operations,
            () => navigation,
            preferences,
            logSink,
            _ => { });

        // Act
        var shouldConfirmPurge = await sut.ShouldConfirmPurgeAsync();
        var shouldConfirmResend = await sut.ShouldConfirmBulkResendAsync();

        // Assert
        shouldConfirmPurge.Should().BeTrue();
        shouldConfirmResend.Should().BeTrue();
    }

    [Fact]
    public async Task GetPurgeConfirmationMessageAsync_WithStrictConfirmation_AddsHighRiskWarning()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.PreviewPurgeMessagesAsync("orders", null, false, Arg.Any<CancellationToken>())
            .Returns(new BulkOperationPreview(
                BulkOperationType.Purge,
                "orders",
                12,
                [],
                []));

        var preferences = Substitute.For<IPreferencesService>();
        preferences.ConfirmBeforePurge.Returns(true);
        var logSink = Substitute.For<ILogSink>();
        var navigation = new NavigationState
        {
            SelectedQueue = CreateQueueInfo("orders")
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
        message.Should().Contain("Dry-run preview");
        message.Should().Contain("cannot be undone");
        sut.GetPurgeConfirmText().Should().Be("Confirm Purge");
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
    public void BuildBulkResendPreview_WithFiltersAndSessions_IncludesStructuredScope()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var preferences = Substitute.For<IPreferencesService>();
        var logSink = Substitute.For<ILogSink>();
        var navigation = new NavigationState
        {
            SelectedQueue = CreateQueueInfo("orders", requiresSession: true),
            SelectedEntity = CreateQueueInfo("orders", requiresSession: true),
            ShowDeadLetter = true,
            EntityFilter = "priority",
            NamespaceFilter = "prod"
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
        var preview = sut.BuildBulkResendPreview(selectedMessages);

        // Assert
        preview.Scope.Should().NotBeNull();
        preview.Scope!.EntityName.Should().Be("orders");
        preview.Scope.SubscriptionName.Should().BeNull();
        preview.Scope.IsDeadLetter.Should().BeTrue();
        preview.Scope.RequiresSession.Should().BeTrue();
        preview.Scope.SessionIds.Should().BeEquivalentTo(["session-a", "session-b"]);
        preview.Scope.SelectedFilters["Entity filter"].Should().Be("priority");
        preview.Scope.SelectedFilters["Namespace filter"].Should().Be("prod");
        preview.SampleMessageIds.Should().ContainInOrder("msg-1", "msg-2");
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
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<BulkOperationProgress>?>())
            .Returns(call =>
            {
                call.ArgAt<IProgress<BulkOperationProgress>?>(5)?.Report(new BulkOperationProgress(
                    BulkOperationType.Delete,
                    ProcessedCount: 1,
                    RequestedCount: 2,
                    Message: "Deleted 1 of 2 message(s)"));

                return new BulkOperationExecutionResult(
                    BulkOperationType.Delete,
                    RequestedCount: 2,
                    SucceededCount: 1,
                    FailedIdentifiers: [new MessageIdentifier(102, "msg-2")],
                    Summary: "Deleted 1 of 2 message(s)",
                    CanResume: true,
                    Failures: [
                        new BulkOperationFailure(
                            new MessageIdentifier(102, "msg-2"),
                            BulkOperationFailureKind.Retryable,
                            "Lock expired")
                    ]);
            });

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
        sut.BulkProgressText.Should().Be("Deleted 1 of 2 message(s)");
        sut.BulkProgressCurrent.Should().Be(1);
        sut.BulkProgressTotal.Should().Be(2);
        statuses.Should().Contain(s => s.Contains("Partial failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FinalSummary_WithRetryableAndNonRetryableFailures_DescribesPartialFailure()
    {
        // Arrange
        var result = new BulkOperationExecutionResult(
            BulkOperationType.Delete,
            RequestedCount: 3,
            SucceededCount: 1,
            FailedIdentifiers: [
                new MessageIdentifier(102, "msg-2"),
                new MessageIdentifier(103, "msg-3")
            ],
            Summary: "Deleted 1 of 3 message(s)",
            CanResume: true,
            Failures: [
                new BulkOperationFailure(
                    new MessageIdentifier(102, "msg-2"),
                    BulkOperationFailureKind.Retryable,
                    "Lock expired"),
                new BulkOperationFailure(
                    new MessageIdentifier(103, "msg-3"),
                    BulkOperationFailureKind.NonRetryable,
                    "Message not found")
            ]);

        // Act
        var summary = result.FinalSummary;

        // Assert
        result.Status.Should().Be(BulkOperationCompletionStatus.PartialFailure);
        result.RetryableFailureCount.Should().Be(1);
        result.NonRetryableFailureCount.Should().Be(1);
        summary.Should().Contain("Partial failure");
        summary.Should().Contain("Retryable: 1");
        summary.Should().Contain("Non-retryable: 1");
    }

    [Fact]
    public void FinalSummary_WhenCancelled_DescribesCancellation()
    {
        // Arrange
        var result = new BulkOperationExecutionResult(
            BulkOperationType.Purge,
            RequestedCount: 10,
            SucceededCount: 4,
            FailedIdentifiers: [],
            Summary: "Purged 4 of 10 message(s)",
            CompletionStatus: BulkOperationCompletionStatus.Cancelled);

        // Assert
        result.Status.Should().Be(BulkOperationCompletionStatus.Cancelled);
        result.FinalSummary.Should().Contain("Cancelled");
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
