namespace BusLane.Tests.ViewModels.Core;

using BusLane.Models;
using BusLane.Services.Monitoring;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using NSubstitute;

public class FeaturePanelsViewModelTests
{
    [Fact]
    public async Task OpenCorrelationExplorer_OpensExplorerAndClosesOtherPanels()
    {
        // Arrange
        var sut = CreateSut();
        await sut.OpenLiveStream();

        // Act
        await sut.OpenCorrelationExplorer();

        // Assert
        sut.ShowCorrelationExplorer.Should().BeTrue();
        sut.CorrelationExplorerViewModel.Should().NotBeNull();
        sut.ShowLiveStream.Should().BeFalse();
        sut.ShowAlerts.Should().BeFalse();
    }

    [Fact]
    public async Task CloseCorrelationExplorer_ClearsExplorerWithoutClearingCatalog()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(new CorrelationMessage(
            CorrelationMessageSource.Loaded,
            "namespace",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            "message-1",
            "corr-1",
            null,
            null,
            "{}",
            DateTimeOffset.UtcNow,
            1,
            new Dictionary<string, object>()));
        var sut = CreateSut(catalog);
        await sut.OpenCorrelationExplorer();

        // Act
        sut.CloseCorrelationExplorer();

        // Assert
        sut.ShowCorrelationExplorer.Should().BeFalse();
        sut.CorrelationExplorerViewModel.Should().BeNull();
        catalog.GetGroups().Should().ContainSingle();
    }

    [Fact]
    public async Task CloseCorrelationExplorer_DisposesExplorerSubscription()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        var delay = Substitute.For<ICorrelationRefreshDelay>();
        var sut = CreateSut(catalog, delay);
        await sut.OpenCorrelationExplorer();

        // Act
        sut.CloseCorrelationExplorer();
        catalog.Add(new CorrelationMessage(
            CorrelationMessageSource.Loaded,
            "namespace",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            "message-1",
            "corr-1",
            null,
            null,
            "{}",
            DateTimeOffset.UtcNow,
            1,
            new Dictionary<string, object>()));

        // Assert
        await delay.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }

    [Fact]
    public async Task OpenLiveStream_WhenExplorerIsOpen_DisposesExplorerSubscription()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        var delay = Substitute.For<ICorrelationRefreshDelay>();
        var sut = CreateSut(catalog, delay);
        await sut.OpenCorrelationExplorer();

        // Act
        await sut.OpenLiveStream();
        catalog.Add(CreateMessage());

        // Assert
        sut.ShowCorrelationExplorer.Should().BeFalse();
        sut.CorrelationExplorerViewModel.Should().BeNull();
        await delay.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }

    [Fact]
    public async Task OpenAlerts_WhenExplorerIsOpen_ClosesExplorer()
    {
        // Arrange
        var sut = CreateSut();
        await sut.OpenCorrelationExplorer();

        // Act
        sut.OpenAlerts();

        // Assert
        sut.ShowCorrelationExplorer.Should().BeFalse();
        sut.CorrelationExplorerViewModel.Should().BeNull();
        sut.ShowAlerts.Should().BeTrue();
    }

    [Fact]
    public async Task OpenLiveStream_AfterClose_WaitsForOldStreamToStopBeforeStarting()
    {
        // Arrange - the old view model's stop is still in flight when the panel is reopened
        var liveStreamService = Substitute.For<ILiveStreamService>();
        var oldStreamStopped = new TaskCompletionSource();
        liveStreamService.StopStreamAsync().Returns(oldStreamStopped.Task, Task.CompletedTask);
        var queue = new QueueInfo("orders", 0, 0, 0, 0, 0, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));
        var sut = CreateSut(liveStreamService: liveStreamService, selectedQueue: queue);
        await sut.OpenLiveStream();
        sut.CloseLiveStream();

        // Act
        var reopen = sut.OpenLiveStream();

        // Assert - the new stream starts only after the old one has stopped
        await liveStreamService.Received(1).StartQueueStreamAsync(Arg.Any<IServiceBusOperations>(), "orders", Arg.Any<CancellationToken>());
        oldStreamStopped.SetResult();
        await reopen;
        await liveStreamService.Received(2).StartQueueStreamAsync(Arg.Any<IServiceBusOperations>(), "orders", Arg.Any<CancellationToken>());
    }

    private static FeaturePanelsViewModel CreateSut(
        ICorrelationMessageCatalog? catalog = null,
        ICorrelationRefreshDelay? refreshDelay = null,
        ILiveStreamService? liveStreamService = null,
        QueueInfo? selectedQueue = null)
    {
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([]);
        var replayService = Substitute.For<IMessageReplayService>();
        var destination = new ReplayDestination("namespace", ConnectionEnvironment.Test, "orders", "Queue", false);
        return new FeaturePanelsViewModel(
            liveStreamService ?? Substitute.For<ILiveStreamService>(),
            Substitute.For<IAlertService>(),
            Substitute.For<INotificationService>(),
            () => Substitute.For<IServiceBusOperations>(),
            () => [],
            () => [],
            () => [],
            () => selectedQueue,
            () => null,
            _ => { },
            catalog ?? new CorrelationMessageCatalog(),
            () => null,
            replayService,
            auditStore,
            () => [destination],
            () => null,
            refreshDelay);
    }

    private static CorrelationMessage CreateMessage() =>
        new(
            CorrelationMessageSource.Loaded,
            "namespace",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            "message-1",
            "corr-1",
            null,
            null,
            "{}",
            DateTimeOffset.UtcNow,
            1,
            new Dictionary<string, object>());
}
