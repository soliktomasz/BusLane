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
        sut.OpenCharts();

        // Act
        await sut.OpenCorrelationExplorer();

        // Assert
        sut.ShowCorrelationExplorer.Should().BeTrue();
        sut.CorrelationExplorerViewModel.Should().NotBeNull();
        sut.ShowCharts.Should().BeFalse();
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

    private static FeaturePanelsViewModel CreateSut(
        ICorrelationMessageCatalog? catalog = null,
        ICorrelationRefreshDelay? refreshDelay = null)
    {
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([]);
        var replayService = Substitute.For<IMessageReplayService>();
        var destination = new ReplayDestination("namespace", ConnectionEnvironment.Test, "orders", "Queue", false);
        var persistence = Substitute.For<BusLane.Services.Dashboard.IDashboardPersistenceService>();
        persistence.Load().Returns(new DashboardConfiguration());
        var dashboard = new DashboardViewModel(
            persistence,
            new BusLane.Services.Dashboard.DashboardLayoutEngine(),
            Substitute.For<BusLane.Services.Monitoring.IMetricsService>());

        return new FeaturePanelsViewModel(
            Substitute.For<ILiveStreamService>(),
            Substitute.For<IAlertService>(),
            Substitute.For<INotificationService>(),
            dashboard,
            () => Substitute.For<IServiceBusOperations>(),
            () => [],
            () => [],
            () => [],
            () => null,
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
}
