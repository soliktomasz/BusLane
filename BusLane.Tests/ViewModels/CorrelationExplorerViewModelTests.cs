namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class CorrelationExplorerViewModelTests
{
    [Fact]
    public async Task RefreshAsync_LoadsGroupsTimelineAndAuditHistory()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("second", 2));
        catalog.Add(CreateMessage("first", 1));
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([
            CreateAuditEntry()
        ]);
        var sut = CreateSut(catalog, auditStore);

        // Act
        await sut.RefreshAsync();

        // Assert
        sut.Groups.Should().ContainSingle();
        sut.SelectedGroup.Should().NotBeNull();
        sut.Timeline.Select(static message => message.MessageId).Should().ContainInOrder("first", "second");
        sut.ReplayHistory.Should().ContainSingle();
    }

    [Fact]
    public async Task FilterText_LimitsCorrelationGroups()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("message-1", 1, "corr-orders"));
        catalog.Add(CreateMessage("message-2", 2, "corr-billing"));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();

        // Act
        sut.FilterText = "corr-orders";

        // Assert
        sut.Groups.Should().ContainSingle().Which.DisplayId.Should().Be("corr-orders");
    }

    [Fact]
    public async Task ApplyFilters_WithStructuredCriteria_FiltersGroupsAndTimeline()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("loaded", 1, "corr-orders"));
        catalog.Add(CreateMessage("streamed", 2, "corr-orders") with
        {
            Source = CorrelationMessageSource.LiveStream,
            EntityName = "orders-v2",
            Environment = ConnectionEnvironment.Production
        });
        catalog.Add(CreateMessage("billing", 3, "corr-billing"));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();
        sut.FilterEntity = "orders-v2";
        sut.FilterEnvironment = ConnectionEnvironment.Production;
        sut.FilterSource = CorrelationMessageSource.LiveStream;

        // Act
        sut.ApplyFiltersCommand.Execute(null);

        // Assert
        sut.Groups.Should().ContainSingle().Which.DisplayId.Should().Be("corr-orders");
        sut.Timeline.Should().ContainSingle().Which.MessageId.Should().Be("streamed");
    }

    [Fact]
    public async Task ApplyFilters_WithInvalidTime_PreservesCurrentResults()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("message-1", 1));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();
        sut.FilterFromText = "not-a-time";

        // Act
        sut.ApplyFiltersCommand.Execute(null);

        // Assert
        sut.FilterValidationMessage.Should().Be("From time must be a valid ISO 8601 timestamp");
        sut.Groups.Should().ContainSingle();
    }

    [Fact]
    public async Task ApplyFilters_WithReversedTimeRange_ShowsValidationAndPreservesResults()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("message-1", 1));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();
        sut.FilterFromText = "2026-07-28T10:00:00Z";
        sut.FilterToText = "2026-07-28T09:00:00Z";

        // Act
        sut.ApplyFiltersCommand.Execute(null);

        // Assert
        sut.FilterValidationMessage.Should().Be("From time must be before or equal to To time");
        sut.Groups.Should().ContainSingle();
    }

    [Fact]
    public async Task ClearFilters_RestoresAllGroups()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("orders", 1, "corr-orders"));
        catalog.Add(CreateMessage("billing", 2, "corr-billing"));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();
        sut.FilterEntity = "orders";
        sut.ApplyFiltersCommand.Execute(null);

        // Act
        sut.ClearFiltersCommand.Execute(null);

        // Assert
        sut.Groups.Should().HaveCount(2);
        sut.FilterEntity.Should().BeNull();
        sut.FilterValidationMessage.Should().BeNull();
    }

    [Fact]
    public void ClearSearchCommand_WithStructuredCriteria_PreservesStructuredFilters()
    {
        // Arrange
        var sut = CreateSut(new CorrelationMessageCatalog(), Substitute.For<IReplayAuditStore>());
        sut.FilterText = "needle";
        sut.FilterNamespace = "orders-prod";
        sut.FilterEnvironment = ConnectionEnvironment.Production;
        sut.FilterIdentifier = "corr-42";

        // Act
        sut.ClearSearchCommand.Execute(null);

        // Assert
        sut.FilterText.Should().BeEmpty();
        sut.FilterNamespace.Should().Be("orders-prod");
        sut.FilterEnvironment.Should().Be(ConnectionEnvironment.Production);
        sut.FilterIdentifier.Should().Be("corr-42");
    }

    [Fact]
    public async Task ApplyFilters_WhenSelectedMessageStillMatches_PreservesSelection()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        catalog.Add(CreateMessage("second", 2));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();
        sut.SelectedMessage = sut.Timeline.Single(message => message.MessageId == "second");
        sut.FilterNamespace = "demo.servicebus.windows.net";

        // Act
        sut.ApplyFiltersCommand.Execute(null);

        // Assert
        sut.SelectedMessage!.MessageId.Should().Be("second");
    }

    [Fact]
    public async Task CatalogChanged_AfterDebounce_RefreshesWithoutStealingSelection()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        var delay = new ControlledRefreshDelay();
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>(), refreshDelay: delay);
        await sut.RefreshAsync();
        sut.SelectedMessage = sut.Timeline.Single();

        // Act
        catalog.Add(CreateMessage("second", 2));

        // Assert
        delay.InvocationCount.Should().Be(1);
        sut.Timeline.Should().ContainSingle();
        delay.ReleaseLatest();
        await WaitUntilAsync(() => sut.Timeline.Count == 2);
        sut.SelectedMessage!.MessageId.Should().Be("first");
        sut.NewMessageCount.Should().Be(1);
    }

    [Fact]
    public async Task CatalogChanged_DuringBurst_CancelsEarlierRefresh()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        var delay = new ControlledRefreshDelay();
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>(), refreshDelay: delay);
        await sut.RefreshAsync();

        // Act
        catalog.Add(CreateMessage("second", 2));
        catalog.Add(CreateMessage("third", 3));
        delay.ReleaseLatest();
        await WaitUntilAsync(() => sut.Timeline.Count == 3);

        // Assert
        delay.InvocationCount.Should().Be(2);
        delay.CancelledCount.Should().Be(1);
        delay.CompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task CatalogChanged_AcrossMultipleBursts_AccumulatesNewMessageCount()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        var delay = new ControlledRefreshDelay();
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>(), refreshDelay: delay);
        await sut.RefreshAsync();

        // Act
        catalog.Add(CreateMessage("second", 2));
        delay.ReleaseLatest();
        await WaitUntilAsync(() => sut.Timeline.Count == 2);
        catalog.Add(CreateMessage("third", 3));
        delay.ReleaseLatest();
        await WaitUntilAsync(() => sut.Timeline.Count == 3);

        // Assert
        sut.NewMessageCount.Should().Be(2);
    }

    [Fact]
    public async Task CatalogChanged_WhenNewMessageIsFilteredOut_DoesNotChangeTimeline()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("orders", 1));
        var delay = new ControlledRefreshDelay();
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>(), refreshDelay: delay);
        await sut.RefreshAsync();
        sut.FilterEntity = "orders";
        sut.ApplyFiltersCommand.Execute(null);

        // Act
        catalog.Add(CreateMessage("billing", 2) with { EntityName = "billing" });
        delay.ReleaseLatest();
        await WaitUntilAsync(() => delay.CompletedCount == 1);

        // Assert
        sut.Timeline.Should().ContainSingle().Which.MessageId.Should().Be("orders");
        sut.NewMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task SelectingNewestMessage_AcknowledgesNewMessages()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        var delay = new ControlledRefreshDelay();
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>(), refreshDelay: delay);
        await sut.RefreshAsync();
        catalog.Add(CreateMessage("second", 2));
        delay.ReleaseLatest();
        await WaitUntilAsync(() => sut.Timeline.Count == 2);

        // Act
        sut.SelectedMessage = sut.Timeline[^1];

        // Assert
        sut.NewMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_UnsubscribesAndCancelsPendingRefresh()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        var delay = new ControlledRefreshDelay();
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>(), refreshDelay: delay);
        await sut.RefreshAsync();
        catalog.Add(CreateMessage("second", 2));

        // Act
        sut.Dispose();
        catalog.Add(CreateMessage("third", 3));

        // Assert
        await WaitUntilAsync(() => delay.CancelledCount == 1);
        delay.InvocationCount.Should().Be(1);
        sut.Timeline.Should().ContainSingle();
    }

    [Fact]
    public async Task SetComparisonMessages_WhenBothAssigned_ProducesComparison()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        catalog.Add(CreateMessage("second", 2) with { Body = """{"status":"changed"}""" });
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();

        // Act
        sut.SetComparisonACommand.Execute(sut.Timeline[0]);
        sut.SetComparisonBCommand.Execute(sut.Timeline[1]);

        // Assert
        sut.ComparisonMessageA!.MessageId.Should().Be("first");
        sut.ComparisonMessageB!.MessageId.Should().Be("second");
        sut.HasComparison.Should().BeTrue();
        sut.Comparison!.Body.IsChanged.Should().BeTrue();
    }

    [Fact]
    public async Task CompareWithPrevious_UsesChronologicalPreviousMessage()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        catalog.Add(CreateMessage("second", 2));
        catalog.Add(CreateMessage("third", 3));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();
        sut.SelectedMessage = sut.Timeline[2];

        // Act
        sut.CompareWithPreviousCommand.Execute(null);

        // Assert
        sut.ComparisonMessageA!.MessageId.Should().Be("second");
        sut.ComparisonMessageB!.MessageId.Should().Be("third");
    }

    [Fact]
    public async Task CompareWithPrevious_ForFirstMessage_ShowsStatus()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();

        // Act
        sut.CompareWithPreviousCommand.Execute(null);

        // Assert
        sut.HasComparison.Should().BeFalse();
        sut.StatusMessage.Should().Be("The selected message has no previous timeline entry");
    }

    [Fact]
    public async Task SetComparisonA_WhenReplacingSlot_RecomputesComparison()
    {
        // Arrange
        var comparisonService = Substitute.For<ICorrelationMessageComparisonService>();
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        catalog.Add(CreateMessage("second", 2));
        catalog.Add(CreateMessage("third", 3));
        var sut = CreateSut(
            catalog,
            Substitute.For<IReplayAuditStore>(),
            comparisonService: comparisonService);
        await sut.RefreshAsync();
        sut.SetComparisonACommand.Execute(sut.Timeline[0]);
        sut.SetComparisonBCommand.Execute(sut.Timeline[1]);

        // Act
        sut.SetComparisonACommand.Execute(sut.Timeline[2]);

        // Assert
        comparisonService.Received(1).Compare(sut.Timeline[2], sut.Timeline[1]);
    }

    [Fact]
    public async Task RefreshAsync_WhenComparedMessageWasEvicted_ClearsOnlyMissingSlot()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog(capacity: 2);
        catalog.Add(CreateMessage("first", 1));
        catalog.Add(CreateMessage("second", 2));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();
        sut.SetComparisonACommand.Execute(sut.Timeline[0]);
        sut.SetComparisonBCommand.Execute(sut.Timeline[1]);
        catalog.Add(CreateMessage("third", 3));

        // Act
        await sut.RefreshAsync();

        // Assert
        sut.ComparisonMessageA.Should().BeNull();
        sut.ComparisonMessageB!.MessageId.Should().Be("second");
        sut.HasComparison.Should().BeFalse();
        sut.StatusMessage.Should().Be("A compared message is no longer available");
    }

    [Fact]
    public async Task ClearComparison_ResetsSlotsAndResult()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("first", 1));
        catalog.Add(CreateMessage("second", 2));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();
        sut.SetComparisonACommand.Execute(sut.Timeline[0]);
        sut.SetComparisonBCommand.Execute(sut.Timeline[1]);

        // Act
        sut.ClearComparisonCommand.Execute(null);

        // Assert
        sut.ComparisonMessageA.Should().BeNull();
        sut.ComparisonMessageB.Should().BeNull();
        sut.Comparison.Should().BeNull();
        sut.HasComparison.Should().BeFalse();
    }

    [Fact]
    public async Task OpenReplay_WithSelectedMessage_CreatesReplayEditor()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("message-1", 1));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();

        // Act
        sut.OpenReplayCommand.Execute(null);

        // Assert
        sut.ReplayEditor.Should().NotBeNull();
        sut.ShowReplayEditor.Should().BeTrue();
    }

    [Fact]
    public async Task ExportHistoryAsync_WritesJsonToSelectedPath()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([CreateAuditEntry()]);
        var path = Path.Combine(Path.GetTempPath(), $"replay-history-{Guid.NewGuid():N}.json");
        var fileDialog = Substitute.For<IFileDialogService>();
        fileDialog.SaveFileAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>())
            .Returns(path);
        var sut = CreateSut(catalog, auditStore, fileDialog);
        await sut.RefreshAsync();

        try
        {
            // Act
            await sut.ExportHistoryCommand.ExecuteAsync(null);

            // Assert
            File.Exists(path).Should().BeTrue();
            (await File.ReadAllTextAsync(path)).Should().Contain("Succeeded");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static CorrelationExplorerViewModel CreateSut(
        ICorrelationMessageCatalog catalog,
        IReplayAuditStore auditStore,
        IFileDialogService? fileDialog = null,
        ICorrelationRefreshDelay? refreshDelay = null,
        ICorrelationMessageComparisonService? comparisonService = null)
    {
        var replayService = Substitute.For<IMessageReplayService>();
        var destination = new ReplayDestination(
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders-replay",
            "Queue",
            false);
        replayService.CreateRequest(Arg.Any<CorrelationMessage>(), destination)
            .Returns(call =>
            {
                var source = call.Arg<CorrelationMessage>()!;
                return new ReplayRequest
                {
                    Source = source,
                    Destination = destination,
                    Body = source.Body,
                    MessageId = "new-id"
                };
            });

        return new CorrelationExplorerViewModel(
            catalog,
            auditStore,
            replayService,
            () => Substitute.For<IServiceBusOperations>(),
            () => [destination],
            fileDialog,
            refreshDelay: refreshDelay,
            comparisonService: comparisonService);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeout = DateTime.UtcNow.AddSeconds(2);
        while (!predicate())
        {
            if (DateTime.UtcNow >= timeout)
            {
                throw new TimeoutException("Condition was not reached");
            }

            await Task.Delay(10);
        }
    }

    private sealed class ControlledRefreshDelay : ICorrelationRefreshDelay
    {
        private readonly List<PendingDelay> _pending = [];

        public int InvocationCount => _pending.Count;
        public int CancelledCount => _pending.Count(item => item.IsCancelled);
        public int CompletedCount => _pending.Count(item => item.IsCompleted);

        public Task DelayAsync(TimeSpan duration, CancellationToken ct = default)
        {
            _ = duration;
            var pending = new PendingDelay(ct);
            _pending.Add(pending);
            return pending.Task;
        }

        public void ReleaseLatest()
        {
            _pending[^1].Release();
        }

        private sealed class PendingDelay
        {
            private readonly TaskCompletionSource _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationTokenRegistration _registration;

            public PendingDelay(CancellationToken ct)
            {
                _registration = ct.Register(() => _completion.TrySetCanceled(ct));
            }

            public Task Task => AwaitAndDisposeAsync();
            public bool IsCancelled => _completion.Task.IsCanceled;
            public bool IsCompleted => _completion.Task.IsCompletedSuccessfully;

            public void Release()
            {
                _completion.TrySetResult();
            }

            private async Task AwaitAndDisposeAsync()
            {
                try
                {
                    await _completion.Task;
                }
                finally
                {
                    _registration.Dispose();
                }
            }
        }
    }

    private static CorrelationMessage CreateMessage(
        string messageId,
        long sequenceNumber,
        string correlationId = "corr-1") =>
        new(
            CorrelationMessageSource.Loaded,
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            messageId,
            correlationId,
            null,
            "application/json",
            "{}",
            DateTimeOffset.Parse("2026-07-28T09:00:00Z").AddMinutes(sequenceNumber),
            sequenceNumber,
            new Dictionary<string, object>());

    private static ReplayAuditEntry CreateAuditEntry() =>
        new(
            "audit-1",
            DateTimeOffset.Parse("2026-07-28T10:00:00Z"),
            ReplayAuditOutcome.Succeeded,
            "message-1",
            "corr-1",
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders-replay",
            false,
            1,
            ["MessageId"],
            [],
            "Message replayed successfully");
}
