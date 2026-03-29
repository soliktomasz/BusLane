namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Models.Logging;
using BusLane.Models.Security;
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
using BusLane.Services.Security;
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

    [Fact]
    public void ActiveWorkspaceModeLabel_WithAzureTab_ReturnsAzureWorkspace()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var activeTab = CreateTab("tab-1", preferences);
        activeTab.IsConnected = true;
        activeTab.Mode = ConnectionMode.AzureAccount;

        // Act
        sut.ActiveTab = activeTab;

        // Assert
        sut.ActiveWorkspaceModeLabel.Should().Be("Azure workspace");
    }

    [Fact]
    public void ActiveWorkspaceModeLabel_WithConnectionStringTab_ReturnsConnectionType()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var activeTab = CreateTab("tab-1", preferences);
        activeTab.IsConnected = true;
        activeTab.Mode = ConnectionMode.ConnectionString;
        activeTab.SavedConnection = SavedConnection.Create(
            "Orders",
            "Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=value",
            ConnectionType.Queue,
            entityName: "orders");

        // Act
        sut.ActiveTab = activeTab;

        // Assert
        sut.ActiveWorkspaceModeLabel.Should().Be("Queue connection");
    }

    [Fact]
    public void ShowNamespaceSelectionPrompt_IsTrueOnlyWhenAzureIsReadyWithoutActiveConnection()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);

        // Act
        sut.Connection.IsAuthenticated = true;
        sut.Connection.CurrentMode = ConnectionMode.AzureAccount;

        // Assert
        sut.ShowNamespaceSelectionPrompt.Should().BeTrue();

        // Act
        var activeTab = CreateTab("tab-1", preferences);
        activeTab.IsConnected = true;
        activeTab.Mode = ConnectionMode.AzureAccount;
        sut.ActiveTab = activeTab;

        // Assert
        sut.ShowNamespaceSelectionPrompt.Should().BeFalse();
    }

    [Fact]
    public void ShowNamespaceSelectionPrompt_WithSelectedNamespaceAndNoActiveTab_RemainsTrue()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var selectedNamespace = new ServiceBusNamespace(
            "ns-1",
            "orders",
            "rg-orders",
            "sub-1",
            "westeurope",
            "sb://orders.servicebus.windows.net/");

        // Act
        sut.Connection.IsAuthenticated = true;
        sut.Connection.CurrentMode = ConnectionMode.AzureAccount;
        sut.Navigation.SelectedNamespace = selectedNamespace;

        // Assert
        sut.ShowNamespaceSelectionPrompt.Should().BeTrue();
    }

    [Fact]
    public void ActiveTabChange_UpdatesConnectionTabActiveFlags()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var firstTab = CreateTab("tab-1", preferences);
        var secondTab = CreateTab("tab-2", preferences);
        sut.ConnectionTabs.Add(firstTab);
        sut.ConnectionTabs.Add(secondTab);

        // Act
        sut.ActiveTab = firstTab;

        // Assert
        GetIsActive(firstTab).Should().BeTrue();
        GetIsActive(secondTab).Should().BeFalse();

        // Act
        sut.ActiveTab = secondTab;

        // Assert
        GetIsActive(firstTab).Should().BeFalse();
        GetIsActive(secondTab).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenAppLockEnabled_ShouldDeferStartupInitialization()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: false));

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        var auth = Substitute.For<IAzureAuthService>();
        var connectionStorage = Substitute.For<IConnectionStorageService>();
        var updateService = Substitute.For<IUpdateService>();

        using var sut = CreateSut(
            preferences,
            auth: auth,
            connectionStorage: connectionStorage,
            updateService: updateService,
            appLockService: appLockService,
            biometricAuthService: biometricAuthService);

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.AppLock.IsLocked.Should().BeTrue();
        await auth.DidNotReceive().TrySilentLoginAsync();
        await connectionStorage.DidNotReceive().GetConnectionsAsync();
        await updateService.DidNotReceive().CheckForUpdatesAsync(Arg.Any<bool>());
    }

    [Fact]
    public async Task UnlockAsync_AfterLockedStartup_ShouldRunStartupOnce()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        preferences.AutoCheckForUpdates = false;
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: false));
        appLockService.VerifyPasswordAsync("Correct#1", Arg.Any<CancellationToken>())
            .Returns(true);

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        var auth = Substitute.For<IAzureAuthService>();
        auth.TrySilentLoginAsync().Returns(false);

        var connectionStorage = Substitute.For<IConnectionStorageService>();
        connectionStorage.GetConnectionsAsync().Returns(Task.FromResult<IEnumerable<SavedConnection>>([]));

        using var sut = CreateSut(
            preferences,
            auth: auth,
            connectionStorage: connectionStorage,
            appLockService: appLockService,
            biometricAuthService: biometricAuthService);

        await sut.InitializeAsync();
        sut.AppLock.Password = "Correct#1";

        // Act
        await sut.AppLock.UnlockCommand.ExecuteAsync(null);
        await sut.InitializeAsync();

        // Assert
        sut.AppLock.IsLocked.Should().BeFalse();
        await auth.Received(1).TrySilentLoginAsync();
        await connectionStorage.Received(1).GetConnectionsAsync();
    }

    [Fact]
    public async Task UnlockAsync_WithWrongPassword_ShouldStayLocked()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: false));
        appLockService.VerifyPasswordAsync("Wrong#1", Arg.Any<CancellationToken>())
            .Returns(false);

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        var auth = Substitute.For<IAzureAuthService>();
        var connectionStorage = Substitute.For<IConnectionStorageService>();

        using var sut = CreateSut(
            preferences,
            auth: auth,
            connectionStorage: connectionStorage,
            appLockService: appLockService,
            biometricAuthService: biometricAuthService);

        await sut.InitializeAsync();
        sut.AppLock.Password = "Wrong#1";

        // Act
        await sut.AppLock.UnlockCommand.ExecuteAsync(null);

        // Assert
        sut.AppLock.IsLocked.Should().BeTrue();
        sut.AppLock.ErrorMessage.Should().Be("Incorrect password.");
        await auth.DidNotReceive().TrySilentLoginAsync();
        await connectionStorage.DidNotReceive().GetConnectionsAsync();
    }

    [Fact]
    public async Task EnableAppLockFromSettings_ShouldKeepCurrentSessionUnlocked()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: false, BiometricUnlockEnabled: false));
        appLockService.EnableAsync(Arg.Any<AppLockConfiguration>(), Arg.Any<CancellationToken>())
            .Returns("ABCD-EFGH-IJKL-MNOP");

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        biometricAuthService.GetAvailabilityAsync(Arg.Any<CancellationToken>())
            .Returns(BiometricAvailability.Available);

        using var sut = CreateSut(
            preferences,
            appLockService: appLockService,
            biometricAuthService: biometricAuthService);

        await sut.InitializeAsync();
        await sut.OpenSettingsCommand.ExecuteAsync(null);

        var settings = sut.SettingsViewModel!.AppLockSettings;
        settings.NewPassword = "Enable#1";
        settings.ConfirmPassword = "Enable#1";
        settings.EnableBiometricUnlock = true;
        settings.HasStoredRecoveryCode = true;

        // Act
        await settings.EnableAppLockCommand.ExecuteAsync(null);

        // Assert
        sut.AppLock.IsEnabled.Should().BeTrue();
        sut.AppLock.IsLocked.Should().BeFalse();
        sut.AppLock.BiometricUnlockEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task OpenSettings_WhenSecurityInitializationIsPending_ShouldNotBlockOpeningDialog()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var snapshotSource = new TaskCompletionSource<AppLockSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(_ => snapshotSource.Task);

        var biometricAuthService = Substitute.For<IBiometricAuthService>();

        using var sut = CreateSut(
            preferences,
            appLockService: appLockService,
            biometricAuthService: biometricAuthService);

        var openTask = Task.Run(() => sut.OpenSettingsCommand.Execute(null));

        try
        {
            await Task.Delay(100);

            openTask.IsCompleted.Should().BeTrue();
            sut.ShowSettings.Should().BeTrue();
            sut.SettingsViewModel.Should().NotBeNull();
        }
        finally
        {
            snapshotSource.TrySetResult(AppLockSnapshot.Disabled);
            await openTask;
        }
    }

    private static MainWindowViewModel CreateSut(
        TestPreferencesService preferences,
        IAzureAuthService? auth = null,
        IConnectionStorageService? connectionStorage = null,
        IUpdateService? updateService = null,
        IAppLockService? appLockService = null,
        IBiometricAuthService? biometricAuthService = null)
    {
        auth ??= Substitute.For<IAzureAuthService>();
        var azureResources = Substitute.For<IAzureResourceService>();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        connectionStorage ??= Substitute.For<IConnectionStorageService>();
        var connectionBackupService = Substitute.For<IConnectionBackupService>();
        var versionService = Substitute.For<IVersionService>();
        var liveStreamService = Substitute.For<ILiveStreamService>();
        var alertService = Substitute.For<IAlertService>();
        var notificationService = Substitute.For<INotificationService>();
        updateService ??= Substitute.For<IUpdateService>();
        var diagnosticBundleService = Substitute.For<IDiagnosticBundleService>();
        var terminalSessionService = Substitute.For<ITerminalSessionService>();
        var ownsAppLockService = appLockService == null;
        var ownsBiometricAuthService = biometricAuthService == null;
        appLockService ??= Substitute.For<IAppLockService>();
        biometricAuthService ??= Substitute.For<IBiometricAuthService>();
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
        if (ownsAppLockService)
            appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(new AppLockSnapshot(IsEnabled: false, BiometricUnlockEnabled: false));

        if (ownsBiometricAuthService)
            biometricAuthService.GetAvailabilityAsync(Arg.Any<CancellationToken>()).Returns(BiometricAvailability.Unavailable);

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
            appLockService,
            biometricAuthService,
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

    private static bool GetIsActive(ConnectionTabViewModel tab)
    {
        var property = typeof(ConnectionTabViewModel).GetProperty("IsActive");
        return property?.GetValue(tab) as bool? ?? false;
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
