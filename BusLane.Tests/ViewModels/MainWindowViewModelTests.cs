namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Models.Logging;
using BusLane.Models.Update;
using BusLane.Services.Abstractions;
using BusLane.Services.Auth;
using BusLane.Services.Dashboard;
using BusLane.Services.Diagnostics;
using BusLane.Services.Infrastructure;
using BusLane.Services.Monitoring;
using BusLane.Services.ServiceBus;
using BusLane.Services.Storage;
using BusLane.Services.Terminal;
using BusLane.Services.Update;
using BusLane.ViewModels;
using BusLane.ViewModels.Core;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using NSubstitute;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class MainWindowViewModelTests
{
    [Fact]
    public void HideEntityPane_WithActiveTab_HidesPaneAndPersistsSessionJson()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var activeTab = CreateTab("tab-1", preferences);
        sut.ConnectionTabs.Add(activeTab);
        sut.ActiveTab = activeTab;

        // Act
        sut.HideEntityPane();

        // Assert
        activeTab.IsEntityPaneVisible.Should().BeFalse();

        var savedStates = DeserializeList<TabSessionState>(preferences.OpenTabsJson);
        savedStates.Should().ContainSingle();
        savedStates[0].IsEntityPaneVisible.Should().BeFalse();
    }

    [Fact]
    public void ShowEntityPane_RestoresOnlyTheActiveTab()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var activeTab = CreateTab("tab-1", preferences, isEntityPaneVisible: false);
        var otherTab = CreateTab("tab-2", preferences, isEntityPaneVisible: false);
        sut.ConnectionTabs.Add(activeTab);
        sut.ConnectionTabs.Add(otherTab);
        sut.ActiveTab = activeTab;

        // Act
        sut.ShowEntityPane();

        // Assert
        activeTab.IsEntityPaneVisible.Should().BeTrue();
        otherTab.IsEntityPaneVisible.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentEntityPaneVisible_TracksTheActiveTab()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var firstTab = CreateTab("tab-1", preferences, isEntityPaneVisible: true);
        var secondTab = CreateTab("tab-2", preferences, isEntityPaneVisible: false);
        sut.ConnectionTabs.Add(firstTab);
        sut.ConnectionTabs.Add(secondTab);

        var changedProperties = new List<string?>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        // Act
        sut.ActiveTab = firstTab;

        // Assert
        sut.IsCurrentEntityPaneVisible.Should().BeTrue();

        // Act
        changedProperties.Clear();
        sut.ActiveTab = secondTab;

        // Assert
        sut.IsCurrentEntityPaneVisible.Should().BeFalse();
        changedProperties.Should().Contain(nameof(MainWindowViewModel.IsCurrentEntityPaneVisible));

        // Act
        changedProperties.Clear();
        secondTab.IsEntityPaneVisible = true;

        // Assert
        sut.IsCurrentEntityPaneVisible.Should().BeTrue();
        changedProperties.Should().Contain(nameof(MainWindowViewModel.IsCurrentEntityPaneVisible));
    }

    private static MainWindowViewModel CreateSut(TestPreferencesService preferences)
    {
        var auth = Substitute.For<IAzureAuthService>();
        var azureResources = Substitute.For<IAzureResourceService>();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var connectionStorage = Substitute.For<IConnectionStorageService>();
        var connectionBackupService = Substitute.For<IConnectionBackupService>();
        var versionService = Substitute.For<IVersionService>();
        var liveStreamService = Substitute.For<ILiveStreamService>();
        var alertService = Substitute.For<IAlertService>();
        var notificationService = Substitute.For<INotificationService>();
        var updateService = Substitute.For<IUpdateService>();
        var diagnosticBundleService = Substitute.For<IDiagnosticBundleService>();
        var terminalSessionService = Substitute.For<ITerminalSessionService>();
        var logSink = CreateLogSink();

        var dashboardPersistenceService = Substitute.For<IDashboardPersistenceService>();
        var dashboardRefreshService = Substitute.For<IDashboardRefreshService>();
        var inboxScoringService = Substitute.For<INamespaceInboxScoringService>();
        var inboxReviewStore = Substitute.For<INamespaceInboxReviewStore>();

        connectionStorage.GetConnectionsAsync().Returns(Task.FromResult<IEnumerable<SavedConnection>>([]));
        dashboardPersistenceService.Load().Returns(new DashboardConfiguration());
        alertService.ActiveAlerts.Returns([]);
        alertService.Rules.Returns([]);
        alertService.History.Returns([]);
        updateService.Status.Returns(UpdateStatus.Idle);
        updateService.AvailableRelease.Returns((ReleaseInfo?)null);
        updateService.ErrorMessage.Returns((string?)null);
        terminalSessionService.SessionId.Returns(Guid.NewGuid());

        var dashboardViewModel = new DashboardViewModel(
            dashboardPersistenceService,
            new DashboardLayoutEngine(),
            new MetricsService());
        var namespaceDashboardViewModel = new NamespaceDashboardViewModel(
            dashboardRefreshService,
            alertService,
            new NamespaceInboxViewModel(inboxScoringService, inboxReviewStore));

        versionService.DisplayVersion.Returns("v1.0.0");

        return new MainWindowViewModel(
            auth,
            azureResources,
            operationsFactory,
            connectionStorage,
            connectionBackupService,
            versionService,
            preferences,
            liveStreamService,
            alertService,
            notificationService,
            new KeyboardShortcutService(),
            updateService,
            diagnosticBundleService,
            terminalSessionService,
            logSink,
            dashboardViewModel,
            namespaceDashboardViewModel);
    }

    private static ConnectionTabViewModel CreateTab(
        string tabId,
        TestPreferencesService preferences,
        bool isEntityPaneVisible = true)
    {
        return new ConnectionTabViewModel(
            tabId,
            $"Tab {tabId}",
            $"{tabId}.servicebus.windows.net",
            preferences,
            CreateLogSink())
        {
            IsEntityPaneVisible = isEntityPaneVisible
        };
    }

    private static ILogSink CreateLogSink()
    {
        var logSink = Substitute.For<ILogSink>();
        logSink.GetLogs().Returns([]);
        return logSink;
    }

    private sealed class TestPreferencesService : IPreferencesService
    {
        public bool ConfirmBeforeDelete { get; set; } = true;
        public bool ConfirmBeforePurge { get; set; } = true;
        public bool AutoRefreshMessages { get; set; }
        public int AutoRefreshIntervalSeconds { get; set; } = 30;
        public int DefaultMessageCount { get; set; } = 100;
        public int MessagesPerPage { get; set; } = 100;
        public int MaxTotalMessages { get; set; } = 500;
        public bool ShowDeadLetterBadges { get; set; } = true;
        public bool EnableMessagePreview { get; set; } = true;
        public bool ShowNavigationPanel { get; set; } = true;
        public bool ShowTerminalPanel { get; set; }
        public bool TerminalIsDocked { get; set; } = true;
        public double TerminalDockHeight { get; set; } = 260;
        public string? TerminalWindowBoundsJson { get; set; }
        public string Theme { get; set; } = "System";
        public int LiveStreamPollingIntervalSeconds { get; set; } = 1;
        public bool RestoreTabsOnStartup { get; set; } = true;
        public string OpenTabsJson { get; set; } = "[]";
        public bool EnableTelemetry { get; set; }
        public bool AutoCheckForUpdates { get; set; } = true;
        public string? SkippedUpdateVersion { get; set; }
        public DateTime? UpdateRemindLaterDate { get; set; }

        public event EventHandler? PreferencesChanged
        {
            add { }
            remove { }
        }

        public void Save()
        {
        }

        public void Load()
        {
        }
    }
}
