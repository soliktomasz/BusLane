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
using System.Reflection;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class MainWindowViewModelTests
{
    [Fact]
    public async Task OpenCorrelationExplorerCommand_WithComposedServices_OpensPanel()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(new CorrelationMessage(
            CorrelationMessageSource.Loaded,
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            "message-1",
            "corr-1",
            null,
            "application/json",
            "{}",
            DateTimeOffset.UtcNow,
            1,
            new Dictionary<string, object>()));
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([]);
        var replayService = Substitute.For<IMessageReplayService>();
        using var sut = CreateSut(
            preferences,
            correlationCatalog: catalog,
            replayAuditStore: auditStore,
            messageReplayService: replayService);

        // Act
        await sut.OpenCorrelationExplorerCommand.ExecuteAsync(null);

        // Assert
        sut.FeaturePanels.ShowCorrelationExplorer.Should().BeTrue();
        sut.FeaturePanels.CorrelationExplorerViewModel.Should().NotBeNull();
        sut.FeaturePanels.CorrelationExplorerViewModel!.Groups.Should().ContainSingle();
    }

    [Fact]
    public async Task OpenCorrelationExplorerCommand_AfterCatalogIngestion_RefreshesLive()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateCorrelationMessage("message-1", 1));
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([]);
        var delay = Substitute.For<ICorrelationRefreshDelay>();
        delay.DelayAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        using var sut = CreateSut(
            preferences,
            correlationCatalog: catalog,
            replayAuditStore: auditStore,
            messageReplayService: Substitute.For<IMessageReplayService>(),
            correlationRefreshDelay: delay,
            correlationComparisonService: new CorrelationMessageComparisonService());
        await sut.OpenCorrelationExplorerCommand.ExecuteAsync(null);

        // Act
        catalog.Add(CreateCorrelationMessage("message-2", 2));
        await WaitUntilAsync(() =>
            sut.FeaturePanels.CorrelationExplorerViewModel?.Timeline.Count == 2);

        // Assert
        sut.FeaturePanels.CorrelationExplorerViewModel!.Timeline
            .Select(static message => message.MessageId)
            .Should().ContainInOrder("message-1", "message-2");
        sut.FeaturePanels.CorrelationExplorerViewModel.SetComparisonACommand.Execute(
            sut.FeaturePanels.CorrelationExplorerViewModel.Timeline[0]);
        sut.FeaturePanels.CorrelationExplorerViewModel.SetComparisonBCommand.Execute(
            sut.FeaturePanels.CorrelationExplorerViewModel.Timeline[1]);
        sut.FeaturePanels.CorrelationExplorerViewModel.HasComparison.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_WhenCorrelationExplorerIsOpen_UnsubscribesFromCatalog()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var catalog = new CorrelationMessageCatalog();
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([]);
        var delay = Substitute.For<ICorrelationRefreshDelay>();
        var sut = CreateSut(
            preferences,
            correlationCatalog: catalog,
            replayAuditStore: auditStore,
            messageReplayService: Substitute.For<IMessageReplayService>(),
            correlationRefreshDelay: delay);
        await sut.OpenCorrelationExplorerCommand.ExecuteAsync(null);

        // Act
        sut.Dispose();
        catalog.Add(CreateCorrelationMessage("message-1", 1));

        // Assert
        await delay.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }

    [Fact]
    public void IntroductionSplash_WithNewPreferences_IsVisible()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);

        // Assert
        sut.ShowIntroductionSplash.Should().BeTrue();
    }

    [Fact]
    public void DismissIntroductionSplash_SavesPreferenceAndHidesSplash()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);

        // Act
        sut.DismissIntroductionSplashCommand.Execute(null);

        // Assert
        sut.ShowIntroductionSplash.Should().BeFalse();
        preferences.HasSeenIntroduction.Should().BeTrue();
        preferences.SaveCount.Should().Be(1);
    }

    [Fact]
    public void IntroductionSplash_WithSeenPreferences_IsHidden()
    {
        // Arrange
        var preferences = new TestPreferencesService
        {
            HasSeenIntroduction = true
        };
        using var sut = CreateSut(preferences);

        // Assert
        sut.ShowIntroductionSplash.Should().BeFalse();
    }

    [Fact]
    public void MoreTools_OnCreation_IsCollapsed()
    {
        // Arrange
        var preferences = new TestPreferencesService();

        // Act
        using var sut = CreateSut(preferences);

        // Assert
        sut.IsMoreToolsExpanded.Should().BeFalse();
    }

    [Fact]
    public void ShowIntroductionSplashCommand_WithSeenPreferences_ShowsSplashAgainWithoutChangingPreference()
    {
        // Arrange
        var preferences = new TestPreferencesService
        {
            HasSeenIntroduction = true
        };
        using var sut = CreateSut(preferences);

        // Act
        sut.ShowIntroductionSplashCommand.Execute(null);

        // Assert
        sut.ShowIntroductionSplash.Should().BeTrue();
        preferences.HasSeenIntroduction.Should().BeTrue();
        preferences.SaveCount.Should().Be(0);
    }

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
    public void ShellStatusMessage_TracksActiveTabStatusMessageChanges()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var activeTab = CreateTab("tab-1", preferences);
        sut.ConnectionTabs.Add(activeTab);
        sut.ActiveTab = activeTab;

        var changedProperties = new List<string?>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        // Act
        activeTab.StatusMessage = "Connected";

        // Assert
        sut.ShellStatusMessage.Should().Be("Connected");
        sut.ShellStatusSummary.Should().Be("Connected");
        changedProperties.Should().Contain(nameof(MainWindowViewModel.ShellStatusMessage));
        changedProperties.Should().Contain(nameof(MainWindowViewModel.ShellStatusSummary));
    }

    [Fact]
    public void ShellStatusSummary_TruncatesVerboseErrorMessages()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var activeTab = CreateTab("tab-1", preferences);
        sut.ConnectionTabs.Add(activeTab);
        sut.ActiveTab = activeTab;

        // Act
        activeTab.StatusMessage = "Connection failed: InvalidSignature: The token has an invalid signature. TrackingId:abc";

        // Assert
        sut.ShellStatusSummary.Should().Be("Connection failed");
        sut.ShellStatusMessage.Should().Contain("InvalidSignature");
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
    public void OpenOverviewCommand_UsesActiveNamespaceWorkspace()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var tab = CreateTab("tab-1", preferences);
        tab.IsConnected = true;
        tab.WorkspaceMode = NamespaceWorkspaceMode.Entity;
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;

        // Act
        sut.OpenOverviewCommand.Execute(null);

        // Assert
        tab.WorkspaceMode.Should().Be(NamespaceWorkspaceMode.Overview);
        sut.IsNamespaceOverviewVisible.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectionStringTab_ActivatesDashboardWhileOverviewIsVisible()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        var dashboardRefreshService = Substitute.For<IDashboardRefreshService>();
        using var sut = CreateSut(
            preferences,
            operationsFactory: operationsFactory,
            dashboardRefreshService: dashboardRefreshService);

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueueInfoAsync("orders", Arg.Any<CancellationToken>())
            .Returns(new QueueInfo(
                "orders",
                12,
                10,
                2,
                0,
                1024,
                DateTimeOffset.UtcNow,
                false,
                TimeSpan.FromDays(14),
                TimeSpan.FromMinutes(1)));

        var tab = CreateTab("tab-1", preferences);
        var connection = SavedConnection.Create(
            "Orders",
            "Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            ConnectionType.Queue,
            entityName: "orders");

        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);
        sut.ConnectionTabs.Add(tab);

        // Act
        sut.ActiveTab = tab;

        // Assert
        _ = dashboardRefreshService.Received(1).RefreshAsync(
            "Orders",
            operations,
            Arg.Any<CancellationToken>());
        dashboardRefreshService.Received(1).StartAutoRefresh(
            "Orders",
            operations,
            TimeSpan.FromSeconds(30));
        sut.IsNamespaceOverviewVisible.Should().BeTrue();
        dashboardRefreshService.ClearReceivedCalls();

        // Act
        tab.WorkspaceMode = NamespaceWorkspaceMode.Entity;

        // Assert
        dashboardRefreshService.Received(1).StopAutoRefresh();
        sut.IsConnectionStringEntityWorkspaceVisible.Should().BeTrue();
        dashboardRefreshService.ClearReceivedCalls();

        // Act
        sut.OpenOverviewCommand.Execute(null);

        // Assert
        _ = dashboardRefreshService.Received(1).RefreshAsync(
            "Orders",
            operations,
            Arg.Any<CancellationToken>());
        dashboardRefreshService.Received(1).StartAutoRefresh(
            "Orders",
            operations,
            TimeSpan.FromSeconds(30));
        sut.IsNamespaceOverviewVisible.Should().BeTrue();
    }

    [Fact]
    public async Task OpenInboxDeadLetter_SwitchesVisibleWorkspaceBeforeMessageLoadCompletes()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        var scoringService = Substitute.For<INamespaceInboxScoringService>();
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource<IEnumerable<MessageInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new QueueInfo(
            "orders",
            12,
            10,
            2,
            0,
            1024,
            DateTimeOffset.UtcNow,
            false,
            TimeSpan.FromDays(14),
            TimeSpan.FromMinutes(1));
        var inboxItem = new NamespaceInboxItem(
            "orders",
            EntityType.Queue,
            null,
            false,
            10,
            2,
            0,
            0,
            20,
            ["Dead letters need attention"]);

        scoringService.Rank(
                Arg.Any<IEnumerable<QueueInfo>>(),
                Arg.Any<IEnumerable<SubscriptionInfo>>(),
                Arg.Any<IEnumerable<AlertEvent>>(),
                Arg.Any<TimeSpan?>())
            .Returns([inboxItem]);
        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueueInfoAsync("orders", Arg.Any<CancellationToken>()).Returns(queue);
        operations.PeekMessagesAsync(
                "orders",
                null,
                Arg.Any<int>(),
                null,
                true,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                loadStarted.TrySetResult();
                return releaseLoad.Task;
            });

        using var sut = CreateSut(
            preferences,
            operationsFactory: operationsFactory,
            inboxScoringService: scoringService);
        var tab = CreateConnectedQueueTab("tab-1", preferences, operationsFactory, "Orders", "orders");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        sut.NamespaceDashboard.Inbox.Refresh("Orders", [queue], [], []);

        // Act
        sut.NamespaceDashboard.Inbox.Items.Single().OpenDeadLetterCommand.Execute(null);
        await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        tab.WorkspaceMode.Should().Be(NamespaceWorkspaceMode.Entity);
        tab.Navigation.SelectedMessageTabIndex.Should().Be(1);
        sut.IsNamespaceOverviewVisible.Should().BeFalse();
        sut.WorkspaceDestinationLabel.Should().Be("Dead letters");

        releaseLoad.SetResult([]);
        await WaitUntilAsync(() => tab.RecentDestinations.Count == 1);
    }

    [Fact]
    public async Task OpenInboxSessions_SwitchesToVisibleSessionInspector()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        var scoringService = Substitute.For<INamespaceInboxScoringService>();
        var queue = new QueueInfo(
            "session-orders", 12, 12, 0, 0, 1024, DateTimeOffset.UtcNow,
            true, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));
        scoringService.Rank(
                Arg.Any<IEnumerable<QueueInfo>>(),
                Arg.Any<IEnumerable<SubscriptionInfo>>(),
                Arg.Any<IEnumerable<AlertEvent>>(),
                Arg.Any<TimeSpan?>())
            .Returns([CreateRankedQueue("session-orders", requiresSession: true)]);
        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueueInfoAsync("session-orders", Arg.Any<CancellationToken>()).Returns(queue);
        operations.GetSessionInspectorItemsAsync("session-orders", null, Arg.Any<CancellationToken>())
            .Returns([]);

        using var sut = CreateSut(
            preferences,
            operationsFactory: operationsFactory,
            inboxScoringService: scoringService);
        var tab = CreateConnectedQueueTab(
            "tab-1", preferences, operationsFactory, "Orders", "session-orders");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        sut.NamespaceDashboard.Inbox.Refresh("Orders", [queue], [], []);

        // Act
        sut.NamespaceDashboard.Inbox.Items.Single().OpenSessionInspectorCommand.Execute(null);

        // Assert
        await WaitUntilAsync(() => tab.StatusMessage == "No sessions discovered");
        tab.WorkspaceMode.Should().Be(NamespaceWorkspaceMode.Entity);
        tab.Navigation.SelectedMessageTabIndex.Should().Be(2);
        sut.IsNamespaceOverviewVisible.Should().BeFalse();
        sut.WorkspaceDestinationLabel.Should().Be("Sessions");
    }

    [Fact]
    public async Task BackToOverview_PreservesSectionAndSearchQuery()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        var queue = CreateQueue("orders");
        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueueInfoAsync("orders", Arg.Any<CancellationToken>()).Returns(queue);
        operations.PeekMessagesAsync(
                "orders", null, Arg.Any<int>(), null, false, false, null, Arg.Any<CancellationToken>())
            .Returns([]);
        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
        var tab = CreateConnectedQueueTab("tab-1", preferences, operationsFactory, "Orders", "orders");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        sut.NamespaceDashboard.SelectedSection = NamespaceOverviewSection.Analytics;
        sut.NamespaceDashboard.EntitySearch.Query = "ord";
        sut.NamespaceDashboard.EntitySearch.SelectedResult = sut.NamespaceDashboard.EntitySearch.Results.Single();
        sut.NamespaceDashboard.EntitySearch.OpenSelectedCommand.Execute(null);
        await WaitUntilAsync(() => tab.WorkspaceMode == NamespaceWorkspaceMode.Entity);

        // Act
        sut.BackToOverviewCommand.Execute(null);

        // Assert
        tab.WorkspaceMode.Should().Be(NamespaceWorkspaceMode.Overview);
        tab.OverviewSection.Should().Be(NamespaceOverviewSection.Analytics);
        sut.NamespaceDashboard.SelectedSection.Should().Be(NamespaceOverviewSection.Analytics);
        sut.NamespaceDashboard.EntitySearch.Query.Should().Be("ord");
    }

    [Fact]
    public void SwitchingTabs_RestoresEachWorkspaceModeAndOverviewSection()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var entityTab = CreateTab("tab-1", preferences);
        entityTab.IsConnected = true;
        entityTab.WorkspaceMode = NamespaceWorkspaceMode.Entity;
        var overviewTab = CreateTab("tab-2", preferences);
        overviewTab.IsConnected = true;
        overviewTab.WorkspaceMode = NamespaceWorkspaceMode.Overview;
        overviewTab.OverviewSection = NamespaceOverviewSection.Issues;
        sut.ConnectionTabs.Add(entityTab);
        sut.ConnectionTabs.Add(overviewTab);

        // Act / Assert
        sut.ActiveTab = overviewTab;
        sut.IsNamespaceOverviewVisible.Should().BeTrue();
        sut.NamespaceDashboard.SelectedSection.Should().Be(NamespaceOverviewSection.Issues);

        sut.ActiveTab = entityTab;
        sut.IsNamespaceOverviewVisible.Should().BeFalse();
        entityTab.WorkspaceMode.Should().Be(NamespaceWorkspaceMode.Entity);

        sut.ActiveTab = overviewTab;
        sut.IsNamespaceOverviewVisible.Should().BeTrue();
        sut.NamespaceDashboard.SelectedSection.Should().Be(NamespaceOverviewSection.Issues);
    }

    [Fact]
    public async Task OpenInboxMessages_WhenSecondRequestWins_DoesNotRestoreFirstDestination()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        var scoringService = Substitute.For<INamespaceInboxScoringService>();
        var firstLoadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstQueue = CreateQueue("queue-a");
        var secondQueue = CreateQueue("queue-b");
        var rankedItems = new[]
        {
            CreateRankedQueue("queue-a"),
            CreateRankedQueue("queue-b")
        };

        scoringService.Rank(
                Arg.Any<IEnumerable<QueueInfo>>(),
                Arg.Any<IEnumerable<SubscriptionInfo>>(),
                Arg.Any<IEnumerable<AlertEvent>>(),
                Arg.Any<TimeSpan?>())
            .Returns(rankedItems);
        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns([firstQueue, secondQueue]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns([]);
        operations.PeekMessagesAsync(
                "queue-a",
                null,
                Arg.Any<int>(),
                null,
                false,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cancellationToken = callInfo.ArgAt<CancellationToken>(7);
                var completion = new TaskCompletionSource<IEnumerable<MessageInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                firstLoadStarted.TrySetResult();
                return completion.Task;
            });
        operations.PeekMessagesAsync(
                "queue-b",
                null,
                Arg.Any<int>(),
                null,
                false,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns([CreateMessage("queue-b-message", 2)]);

        using var sut = CreateSut(
            preferences,
            operationsFactory: operationsFactory,
            inboxScoringService: scoringService);
        var tab = CreateTab("tab-1", preferences);
        var connection = SavedConnection.Create(
            "Orders",
            "Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            ConnectionType.Namespace);
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        sut.NamespaceDashboard.Inbox.Refresh("Orders", [firstQueue, secondQueue], [], []);

        // Act
        sut.NamespaceDashboard.Inbox.Items.Single(item => item.EntityName == "queue-a")
            .OpenMessagesCommand.Execute(null);
        await firstLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        sut.NamespaceDashboard.Inbox.Items.Single(item => item.EntityName == "queue-b")
            .OpenMessagesCommand.Execute(null);

        // Assert
        await WaitUntilAsync(() => tab.MessageOps.Messages.Any(message => message.MessageId == "queue-b-message"));
        tab.Navigation.SelectedQueue.Should().Be(secondQueue);
        tab.CurrentDestination!.EntityName.Should().Be("queue-b");
        tab.RecentDestinations.Should().ContainSingle();
        tab.RecentDestinations[0].Request.EntityName.Should().Be("queue-b");
    }

    [Fact]
    public async Task OpenInboxSubscription_WhenEntityDisappears_KeepsDestinationVisibleWithError()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        var scoringService = Substitute.For<INamespaceInboxScoringService>();
        var topic = new TopicInfo("topic-a", 1024, 1, null, TimeSpan.FromDays(14));
        var inboxItem = new NamespaceInboxItem(
            "topic-a/sub-a",
            EntityType.Subscription,
            "topic-a",
            false,
            1,
            1,
            0,
            0,
            10,
            ["Dead letters need attention"]);

        scoringService.Rank(
                Arg.Any<IEnumerable<QueueInfo>>(),
                Arg.Any<IEnumerable<SubscriptionInfo>>(),
                Arg.Any<IEnumerable<AlertEvent>>(),
                Arg.Any<TimeSpan?>())
            .Returns([inboxItem]);
        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetTopicInfoAsync("topic-a", Arg.Any<CancellationToken>()).Returns(topic);
        operations.GetSubscriptionsAsync("topic-a", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IEnumerable<SubscriptionInfo>>([]),
                Task.FromException<IEnumerable<SubscriptionInfo>>(new InvalidOperationException("entity missing")));

        using var sut = CreateSut(
            preferences,
            operationsFactory: operationsFactory,
            inboxScoringService: scoringService);
        var tab = CreateConnectedTopicTab("tab-1", preferences, operationsFactory, operations, "topic-a");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        sut.NamespaceDashboard.Inbox.Refresh("Orders", [], [], []);

        // Act
        sut.NamespaceDashboard.Inbox.Items.Single().OpenDeadLetterCommand.Execute(null);

        // Assert
        await WaitUntilAsync(() => tab.StatusMessage?.StartsWith("Unable to open", StringComparison.Ordinal) == true);
        tab.WorkspaceMode.Should().Be(NamespaceWorkspaceMode.Entity);
        tab.CurrentDestination!.EntityName.Should().Be("topic-a/sub-a");
        tab.RecentDestinations.Should().BeEmpty();
    }

    [Fact]
    public async Task ToggleDeadLetterViewAsync_WithActiveQueue_LoadsMessagesOnce()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        var messageLoadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueueInfoAsync("orders", Arg.Any<CancellationToken>())
            .Returns(new QueueInfo(
                "orders",
                12,
                10,
                2,
                0,
                1024,
                DateTimeOffset.UtcNow,
                false,
                TimeSpan.FromDays(14),
                TimeSpan.FromMinutes(1)));
        operations.PeekMessagesAsync(
                "orders",
                null,
                Arg.Any<int>(),
                null,
                true,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                messageLoadStarted.TrySetResult();
                return Task.FromResult<IEnumerable<MessageInfo>>(Array.Empty<MessageInfo>());
            });

        var tab = CreateConnectedQueueTab("tab-1", preferences, operationsFactory, connectionName: "Orders", entityName: "orders");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;

        // Act
        await sut.ToggleDeadLetterViewCommand.ExecuteAsync(null);
        await messageLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        await operations.Received(1).PeekMessagesAsync(
            "orders",
            null,
            preferences.MessagesPerPage,
            null,
            true,
            false,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WithLoadedQueueMessages_ReloadsMessagesAfterEntitiesRefresh()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueueInfoAsync("orders", Arg.Any<CancellationToken>())
            .Returns(new QueueInfo(
                "orders",
                12,
                10,
                2,
                0,
                1024,
                DateTimeOffset.UtcNow,
                false,
                TimeSpan.FromDays(14),
                TimeSpan.FromMinutes(1)));
        operations.PeekMessagesAsync(
                "orders",
                null,
                Arg.Any<int>(),
                null,
                false,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IEnumerable<MessageInfo>>([CreateMessage("old-message", 1)]),
                Task.FromResult<IEnumerable<MessageInfo>>([CreateMessage("refreshed-message", 2)]));

        var tab = CreateConnectedQueueTab("tab-1", preferences, operationsFactory, connectionName: "Orders", entityName: "orders");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        await sut.LoadMessagesCommand.ExecuteAsync(null);

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        tab.MessageOps.Messages.Should().ContainSingle(message => message.MessageId == "refreshed-message");
        await operations.Received(2).PeekMessagesAsync(
            "orders",
            null,
            preferences.MessagesPerPage,
            null,
            false,
            false,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkDeleteMessagesAsync_WhenMessageIsDeleted_RefreshesSelectedQueueCount()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
        var selectedMessage = CreateMessage("message-1", 1);

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueueInfoAsync("orders", Arg.Any<CancellationToken>())
            .Returns(
                new QueueInfo("orders", 2, 2, 0, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1)),
                new QueueInfo("orders", 1, 1, 0, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1)));
        operations.DeleteMessagesDetailedAsync(
                "orders",
                null,
                Arg.Any<IEnumerable<MessageIdentifier>>(),
                false,
                progress: Arg.Any<IProgress<BulkOperationProgress>?>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(new BulkOperationExecutionResult(
                BulkOperationType.Delete,
                RequestedCount: 1,
                SucceededCount: 1,
                FailedIdentifiers: [],
                Summary: "Deleted 1 of 1 message(s)"));
        operations.PeekMessagesAsync(
                "orders",
                null,
                Arg.Any<int>(),
                null,
                false,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<MessageInfo>>([CreateMessage("message-2", 2)]));

        var tab = CreateConnectedQueueTab("tab-1", preferences, operationsFactory, connectionName: "Orders", entityName: "orders");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        tab.MessageOps.SelectedMessages.Add(selectedMessage);

        // Act
        sut.BulkDeleteMessagesAsyncCommand.Execute(null);
        await sut.Confirmation.ExecuteConfirmDialogAsync();

        // Assert
        tab.Navigation.SelectedQueue!.ActiveMessageCount.Should().Be(1);
        tab.Navigation.Queues.Should().ContainSingle(queue => queue.ActiveMessageCount == 1);
        tab.MessageOps.Pagination.PageDetailText.Should().Be("of 1 messages");
        await operations.Received(2).GetQueueInfoAsync("orders", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoRefresh_WhenPreviousTickIsStillRunning_SkipsOverlappingAlertEvaluation()
    {
        // Arrange
        var preferences = new TestPreferencesService
        {
            AutoRefreshMessages = false
        };
        var alertService = Substitute.For<IAlertService>();
        var activeAlertEvaluations = 0;
        var maxConcurrentAlertEvaluations = 0;
        var firstEvaluationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowEvaluationToComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sut = CreateSut(
            preferences,
            alertService: alertService);

        sut.Navigation.Queues.Add(new QueueInfo(
            "orders",
            12,
            10,
            2,
            0,
            1024,
            DateTimeOffset.UtcNow,
            false,
            TimeSpan.FromDays(14),
            TimeSpan.FromMinutes(1)));
        alertService.EvaluateAlertsAsync(Arg.Any<IEnumerable<QueueInfo>>(), Arg.Any<IEnumerable<SubscriptionInfo>>())
            .Returns(async _ =>
            {
                var inFlight = Interlocked.Increment(ref activeAlertEvaluations);
                UpdateMaxValue(ref maxConcurrentAlertEvaluations, inFlight);
                firstEvaluationStarted.TrySetResult();

                try
                {
                    await allowEvaluationToComplete.Task;
                    return Enumerable.Empty<AlertEvent>();
                }
                finally
                {
                    Interlocked.Decrement(ref activeAlertEvaluations);
                }
            });

        // Act
        var firstTick = InvokeHandleAutoRefreshTickAsync(sut);
        await firstEvaluationStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var secondTick = InvokeHandleAutoRefreshTickAsync(sut);
        await secondTick.WaitAsync(TimeSpan.FromSeconds(1));
        allowEvaluationToComplete.TrySetResult();
        await firstTick;

        // Assert
        maxConcurrentAlertEvaluations.Should().Be(1);
        await alertService.Received(1).EvaluateAlertsAsync(
            Arg.Any<IEnumerable<QueueInfo>>(),
            Arg.Any<IEnumerable<SubscriptionInfo>>());
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
    public async Task UnlockAsync_WhenStartupInitializationFails_ShouldSetStatusMessageWithoutThrowing()
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
        auth.TrySilentLoginAsync().Returns(Task.FromException<bool>(new InvalidOperationException("Silent login failed")));

        using var sut = CreateSut(
            preferences,
            auth: auth,
            appLockService: appLockService,
            biometricAuthService: biometricAuthService);

        await sut.InitializeAsync();
        sut.AppLock.Password = "Correct#1";

        // Act
        await sut.AppLock.UnlockCommand.ExecuteAsync(null);

        // Assert
        sut.AppLock.IsLocked.Should().BeFalse();
        sut.StatusMessage.Should().Be("Unable to finish startup after unlock: Silent login failed");
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

    [Fact]
    public void OpenCommandPalette_ShouldExposeCommonActionsAndSavedConnections()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var connection = SavedConnection.Create(
            "Orders",
            "Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=value",
            ConnectionType.Queue,
            entityName: "orders");
        sut.Connection.SavedConnections.Add(connection);

        // Act
        sut.OpenCommandPaletteCommand.Execute(null);

        // Assert
        sut.CommandPalette.IsOpen.Should().BeTrue();
        sut.CommandPalette.FilteredItems.Select(item => item.Title)
            .Should().Contain([
                "Open My Connections",
                "Open Settings",
                "Connect to Orders"
            ]);
    }

    [Fact]
    public async Task ExecuteCommandPaletteItem_WithSettingsAction_ShouldClosePaletteAndOpenSettings()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        sut.OpenCommandPaletteCommand.Execute(null);
        sut.CommandPalette.SearchText = "settings";
        var item = sut.CommandPalette.FilteredItems.Should().ContainSingle().Subject;

        // Act
        await sut.ExecuteCommandPaletteItemCommand.ExecuteAsync(item);

        // Assert
        sut.CommandPalette.IsOpen.Should().BeFalse();
        sut.ShowSettings.Should().BeTrue();
        sut.SettingsViewModel.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteCommandPaletteItem_WhenActionFails_ShouldSetStatusMessageWithoutThrowing()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        sut.OpenCommandPaletteCommand.Execute(null);
        var item = new CommandPaletteItem(
            "Failing Command",
            "Throws during execution",
            "Test",
            "AlertTriangle",
            () => throw new InvalidOperationException("Command failed"));

        // Act
        var act = () => sut.ExecuteCommandPaletteItemCommand.ExecuteAsync(item);

        // Assert
        await act.Should().NotThrowAsync();
        sut.CommandPalette.IsOpen.Should().BeFalse();
        sut.StatusMessage.Should().Be("Command failed: Command failed");
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithBlankName_ShowsValidationAndDoesNotCallOperations()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operations = Substitute.For<IConnectionStringOperations>();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetTopicInfoAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(new TopicInfo("orders-topic", 1024, 0, null, TimeSpan.FromDays(14)));
        operations.GetSubscriptionsAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<SubscriptionInfo>>([]));

        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
        var tab = CreateConnectedTopicTab("tab-1", preferences, operationsFactory, operations, "orders-topic");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        var topic = tab.Navigation.Topics.Single();

        // Act
        sut.OpenCreateSubscriptionDialogCommand.Execute(topic);
        sut.NewSubscriptionName = " ";
        await sut.CreateSubscriptionCommand.ExecuteAsync(null);

        // Assert
        sut.ShowCreateSubscriptionDialog.Should().BeTrue();
        sut.StatusMessage.Should().Be("Subscription name is required");
        await operations.DidNotReceive().CreateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Any<SubscriptionCreationOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithValidName_CreatesReloadsAndSelectsSubscription()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operations = Substitute.For<IConnectionStringOperations>();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var initialSubscriptions = new[]
        {
            new SubscriptionInfo("existing", "orders-topic", 0, 0, 0, null, false)
        };
        var refreshedSubscriptions = new[]
        {
            initialSubscriptions[0],
            new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, true)
        };

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetTopicInfoAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(new TopicInfo("orders-topic", 1024, 1, null, TimeSpan.FromDays(14)));
        operations.GetSubscriptionsAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IEnumerable<SubscriptionInfo>>(initialSubscriptions),
                Task.FromResult<IEnumerable<SubscriptionInfo>>(refreshedSubscriptions));
        operations.PeekMessagesAsync(
                "orders-topic",
                "processor",
                Arg.Any<int>(),
                null,
                false,
                true,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<MessageInfo>>([]));

        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
        var tab = CreateConnectedTopicTab("tab-1", preferences, operationsFactory, operations, "orders-topic");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        var topic = tab.Navigation.Topics.Single();

        // Act
        sut.OpenCreateSubscriptionDialogCommand.Execute(topic);
        sut.NewSubscriptionName = "processor";
        sut.NewSubscriptionRequiresSession = true;
        await sut.CreateSubscriptionCommand.ExecuteAsync(null);

        // Assert
        await operations.Received(1).CreateSubscriptionAsync(
            "orders-topic",
            Arg.Is<SubscriptionCreationOptions>(options =>
                options.Name == "processor" &&
                options.RequiresSession),
            Arg.Any<CancellationToken>());
        sut.ShowCreateSubscriptionDialog.Should().BeFalse();
        topic.IsExpanded.Should().BeTrue();
        topic.SubscriptionCount.Should().Be(2);
        topic.Subscriptions.Should().ContainSingle(subscription => subscription.Name == "processor");
        tab.Navigation.SelectedSubscription.Should().NotBeNull();
        tab.Navigation.SelectedSubscription!.Name.Should().Be("processor");
        sut.StatusMessage.Should().Be("Subscription 'processor' created");
    }

    [Fact]
    public async Task SelectSubscriptionAsync_ClearsSelectedTopic()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operations = Substitute.For<IConnectionStringOperations>();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var subscription = new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false);

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetTopicInfoAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(new TopicInfo("orders-topic", 1024, 1, null, TimeSpan.FromDays(14)));
        operations.GetSubscriptionsAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<SubscriptionInfo>>([subscription]));
        operations.PeekMessagesAsync(
                "orders-topic",
                "processor",
                Arg.Any<int>(),
                null,
                false,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<MessageInfo>>([]));

        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
        var tab = CreateConnectedTopicTab("tab-1", preferences, operationsFactory, operations, "orders-topic");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        var topic = tab.Navigation.Topics.Single();
        tab.Navigation.SelectedTopic = topic;

        // Act
        await sut.SelectSubscriptionCommand.ExecuteAsync(subscription);

        // Assert
        tab.Navigation.SelectedTopic.Should().BeNull();
        tab.Navigation.SelectedSubscription.Should().Be(subscription);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WhenActiveTabChanges_UsesOriginalWorkspaceOperations()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var originalOperations = Substitute.For<IConnectionStringOperations>();
        var otherOperations = Substitute.For<IConnectionStringOperations>();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var refreshedSubscriptions = new[]
        {
            new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false)
        };

        operationsFactory.CreateFromConnectionString(Arg.Any<string>())
            .Returns(originalOperations, otherOperations);
        originalOperations.GetTopicInfoAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(new TopicInfo("orders-topic", 1024, 0, null, TimeSpan.FromDays(14)));
        originalOperations.GetSubscriptionsAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IEnumerable<SubscriptionInfo>>([]),
                Task.FromResult<IEnumerable<SubscriptionInfo>>(refreshedSubscriptions));
        originalOperations.PeekMessagesAsync(
                "orders-topic",
                "processor",
                Arg.Any<int>(),
                null,
                false,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<MessageInfo>>([]));
        otherOperations.GetQueueInfoAsync("billing", Arg.Any<CancellationToken>())
            .Returns(new QueueInfo("billing", 0, 0, 0, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1)));

        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
        var originalTab = CreateConnectedTopicTab("tab-1", preferences, operationsFactory, originalOperations, "orders-topic");
        var otherTab = CreateConnectedQueueTab("tab-2", preferences, operationsFactory, "Billing", "billing");
        sut.ConnectionTabs.Add(originalTab);
        sut.ConnectionTabs.Add(otherTab);
        sut.ActiveTab = originalTab;
        var topic = originalTab.Navigation.Topics.Single();

        // Act
        sut.OpenCreateSubscriptionDialogCommand.Execute(topic);
        sut.NewSubscriptionName = "processor";
        sut.ActiveTab = otherTab;
        await sut.CreateSubscriptionCommand.ExecuteAsync(null);

        // Assert
        await originalOperations.Received(1).CreateSubscriptionAsync(
            "orders-topic",
            Arg.Is<SubscriptionCreationOptions>(options => options.Name == "processor"),
            Arg.Any<CancellationToken>());
        await otherOperations.DidNotReceive().CreateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Any<SubscriptionCreationOptions>(),
            Arg.Any<CancellationToken>());
        originalTab.Navigation.SelectedSubscription.Should().NotBeNull();
        originalTab.Navigation.SelectedSubscription!.Name.Should().Be("processor");
        otherTab.Navigation.SelectedSubscription.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_WhenDeletingSelectedSubscription_ClearsLoadedMessages()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var operations = Substitute.For<IConnectionStringOperations>();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var subscription = new SubscriptionInfo("processor", "orders-topic", 1, 1, 0, null, false);
        var message = new MessageInfo(
            "message-1",
            null,
            null,
            "{}",
            DateTimeOffset.UtcNow,
            null,
            1,
            1,
            null,
            new Dictionary<string, object>());

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetTopicInfoAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(new TopicInfo("orders-topic", 1024, 1, null, TimeSpan.FromDays(14)));
        operations.GetSubscriptionsAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IEnumerable<SubscriptionInfo>>([subscription]),
                Task.FromResult<IEnumerable<SubscriptionInfo>>([]));

        using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
        var tab = CreateConnectedTopicTab("tab-1", preferences, operationsFactory, operations, "orders-topic");
        sut.ConnectionTabs.Add(tab);
        sut.ActiveTab = tab;
        tab.Navigation.SelectedSubscription = subscription;
        tab.Navigation.SelectedEntity = subscription;
        tab.MessageOps.Messages.Add(message);
        tab.MessageOps.FilteredMessages.Add(message);
        tab.MessageOps.SelectedMessage = message;

        // Act
        sut.DeleteSubscriptionRequestCommand.Execute(subscription);
        await sut.Confirmation.ExecuteConfirmDialogAsync();

        // Assert
        tab.Navigation.SelectedSubscription.Should().BeNull();
        tab.Navigation.SelectedEntity.Should().BeNull();
        tab.Navigation.Topics.Single().SubscriptionCount.Should().Be(0);
        tab.MessageOps.Messages.Should().BeEmpty();
        tab.MessageOps.FilteredMessages.Should().BeEmpty();
        tab.MessageOps.SelectedMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExportMessageAsync_AfterFileDialogServiceIsSet_UsesDialogService()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var exportPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var fileDialogService = Substitute.For<IFileDialogService>();
        fileDialogService.SaveFileAsync(
                "Export Message",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>())
            .Returns(exportPath);

        sut.SetFileDialogService(fileDialogService);
        var message = new MessageInfo(
            "msg-1",
            "corr-1",
            "application/json",
            "{\"ok\":true}",
            DateTimeOffset.UtcNow,
            null,
            42,
            0,
            null,
            new Dictionary<string, object>());

        try
        {
            // Act
            await sut.ExportMessageCommand.ExecuteAsync(message);

            // Assert
            await fileDialogService.Received(1).SaveFileAsync(
                "Export Message",
                Arg.Is<string>(name => name.StartsWith("Message_msg-1_", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)),
                Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>());
            File.Exists(exportPath).Should().BeTrue();
            sut.StatusMessage.Should().Be($"Exported message to {Path.GetFileName(exportPath)}");
        }
        finally
        {
            if (File.Exists(exportPath))
                File.Delete(exportPath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportSelectedMessagesAsync_WithSelectedMessages_ExportsRequestedContent(bool bodyOnly)
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var exportPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var fileDialogService = Substitute.For<IFileDialogService>();
        fileDialogService.SaveFileAsync(
                "Export Selected Messages",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>())
            .Returns(exportPath);

        sut.SetFileDialogService(fileDialogService);
        var body = "{\r\n  \"ok\": true\r\n}";
        sut.CurrentMessageOps.SelectedMessages.Add(new MessageInfo(
            "msg-1",
            "corr-1",
            "application/json",
            body,
            DateTimeOffset.UtcNow,
            null,
            42,
            0,
            null,
            new Dictionary<string, object> { ["source"] = "test" }));

        try
        {
            // Act
            if (bodyOnly)
                await sut.ExportSelectedMessageBodiesCommand.ExecuteAsync(null);
            else
                await sut.ExportSelectedMessagesCommand.ExecuteAsync(null);

            // Assert
            var json = await File.ReadAllTextAsync(exportPath);
            if (bodyOnly)
            {
                json.Should().Be(body);
                json.Should().NotContain("\\r\\n");
                json.Should().NotContain("\\u0022");
            }
            else
            {
                var container = System.Text.Json.JsonSerializer.Deserialize<MessageExportContainer>(json);
                container.Should().NotBeNull();
                container!.Messages.Should().ContainSingle(message =>
                    message.MessageId == "msg-1" &&
                    message.Body == body &&
                    message.CustomProperties["source"] == "test");
            }

            sut.StatusMessage.Should().Be($"Exported 1 selected message(s) to {Path.GetFileName(exportPath)}");
        }
        finally
        {
            if (File.Exists(exportPath))
                File.Delete(exportPath);
        }
    }

    [Fact]
    public async Task ExportSelectedMessageBodiesAsync_WithMultipleJsonBodies_ExportsJsonValuesWithoutStringEscaping()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        using var sut = CreateSut(preferences);
        var exportPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var fileDialogService = Substitute.For<IFileDialogService>();
        fileDialogService.SaveFileAsync(
                "Export Selected Messages",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>())
            .Returns(exportPath);

        sut.SetFileDialogService(fileDialogService);
        foreach (var (id, body) in new[]
                 {
                     ("msg-1", "{\r\n  \"id\": 1\r\n}"),
                     ("msg-2", "{\r\n  \"id\": 2\r\n}")
                 })
        {
            sut.CurrentMessageOps.SelectedMessages.Add(new MessageInfo(
                id, null, "application/json", body, DateTimeOffset.UtcNow,
                null, 0, 0, null, new Dictionary<string, object>()));
        }

        try
        {
            // Act
            await sut.ExportSelectedMessageBodiesCommand.ExecuteAsync(null);

            // Assert
            var json = await File.ReadAllTextAsync(exportPath);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            document.RootElement.GetArrayLength().Should().Be(2);
            document.RootElement[0].GetProperty("id").GetInt32().Should().Be(1);
            document.RootElement[1].GetProperty("id").GetInt32().Should().Be(2);
            json.Should().NotContain("\\r\\n");
            json.Should().NotContain("\\u0022");
        }
        finally
        {
            if (File.Exists(exportPath))
                File.Delete(exportPath);
        }
    }

    private static MainWindowViewModel CreateSut(
        TestPreferencesService preferences,
        IAzureAuthService? auth = null,
        IConnectionStorageService? connectionStorage = null,
        IUpdateService? updateService = null,
        IAppLockService? appLockService = null,
        IBiometricAuthService? biometricAuthService = null,
        IServiceBusOperationsFactory? operationsFactory = null,
        IDashboardRefreshService? dashboardRefreshService = null,
        IAlertService? alertService = null,
        ICorrelationMessageCatalog? correlationCatalog = null,
        IReplayAuditStore? replayAuditStore = null,
        IMessageReplayService? messageReplayService = null,
        ICorrelationRefreshDelay? correlationRefreshDelay = null,
        ICorrelationMessageComparisonService? correlationComparisonService = null,
        INamespaceInboxScoringService? inboxScoringService = null)
    {
        auth ??= Substitute.For<IAzureAuthService>();
        var azureResources = Substitute.For<IAzureResourceService>();
        operationsFactory ??= Substitute.For<IServiceBusOperationsFactory>();
        connectionStorage ??= Substitute.For<IConnectionStorageService>();
        var connectionBackupService = Substitute.For<IConnectionBackupService>();
        var versionService = Substitute.For<IVersionService>();
        var liveStreamService = Substitute.For<ILiveStreamService>();
        alertService ??= Substitute.For<IAlertService>();
        var notificationService = Substitute.For<INotificationService>();
        updateService ??= Substitute.For<IUpdateService>();
        var diagnosticBundleService = Substitute.For<IDiagnosticBundleService>();
        var terminalSessionService = Substitute.For<ITerminalSessionService>();
        var ownsAppLockService = appLockService == null;
        var ownsBiometricAuthService = biometricAuthService == null;
        appLockService ??= Substitute.For<IAppLockService>();
        biometricAuthService ??= Substitute.For<IBiometricAuthService>();
        var logSink = CreateLogSink();

        dashboardRefreshService ??= Substitute.For<IDashboardRefreshService>();
        inboxScoringService ??= Substitute.For<INamespaceInboxScoringService>();
        var inboxReviewStore = Substitute.For<INamespaceInboxReviewStore>();

        connectionStorage.GetConnectionsAsync().Returns(Task.FromResult<IEnumerable<SavedConnection>>([]));
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
            namespaceDashboardViewModel,
            correlationMessageCatalog: correlationCatalog,
            replayAuditStore: replayAuditStore,
            messageReplayService: messageReplayService,
            correlationRefreshDelay: correlationRefreshDelay,
            correlationComparisonService: correlationComparisonService);
    }

    private static CorrelationMessage CreateCorrelationMessage(string messageId, long sequenceNumber) =>
        new(
            CorrelationMessageSource.Loaded,
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            messageId,
            "corr-1",
            null,
            "application/json",
            "{}",
            DateTimeOffset.Parse("2026-07-28T09:00:00Z").AddSeconds(sequenceNumber),
            sequenceNumber,
            new Dictionary<string, object>());

    private static QueueInfo CreateQueue(string name) =>
        new(
            name,
            MessageCount: 1,
            ActiveMessageCount: 1,
            DeadLetterCount: 0,
            ScheduledCount: 0,
            SizeInBytes: 128,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: false,
            DefaultMessageTtl: TimeSpan.FromDays(14),
            LockDuration: TimeSpan.FromMinutes(1));

    private static NamespaceInboxItem CreateRankedQueue(string name, bool requiresSession = false) =>
        new(
            name,
            EntityType.Queue,
            TopicName: null,
            RequiresSession: requiresSession,
            ActiveMessageCount: 1,
            DeadLetterCount: 0,
            ScheduledCount: 0,
            ActiveAlertCount: 1,
            Score: 10,
            Reasons: ["Active alert"]);

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

    private static ConnectionTabViewModel CreateConnectedQueueTab(
        string tabId,
        TestPreferencesService preferences,
        IServiceBusOperationsFactory operationsFactory,
        string connectionName,
        string entityName)
    {
        var tab = CreateTab(tabId, preferences);
        var connection = SavedConnection.Create(
            connectionName,
            "Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            ConnectionType.Queue,
            entityName: entityName);

        tab.ConnectWithConnectionStringAsync(connection, operationsFactory).GetAwaiter().GetResult();
        return tab;
    }

    private static ConnectionTabViewModel CreateConnectedTopicTab(
        string tabId,
        TestPreferencesService preferences,
        IServiceBusOperationsFactory operationsFactory,
        IServiceBusOperations operations,
        string topicName)
    {
        var tab = CreateTab(tabId, preferences);
        var connection = SavedConnection.Create(
            "Orders Topic",
            "Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            ConnectionType.Topic,
            entityName: topicName);

        tab.ConnectWithConnectionStringAsync(connection, operationsFactory).GetAwaiter().GetResult();
        return tab;
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

    private static Task InvokeHandleAutoRefreshTickAsync(MainWindowViewModel sut)
    {
        var method = typeof(MainWindowViewModel).GetMethod("HandleAutoRefreshTickAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (Task)method!.Invoke(sut, [])!;
    }

    private static MessageInfo CreateMessage(string messageId, long sequenceNumber)
    {
        return new MessageInfo(
            messageId,
            null,
            null,
            $"body-{messageId}",
            DateTimeOffset.UtcNow,
            null,
            sequenceNumber,
            0,
            null,
            new Dictionary<string, object>());
    }

    private sealed class TestPreferencesService : IPreferencesService
    {
        public bool ConfirmBeforeDelete { get; set; } = true;
        public bool ConfirmBeforePurge { get; set; } = true;
        public bool AutoRefreshMessages { get; set; }
        public int AutoRefreshIntervalSeconds { get; set; } = 30;
        public int DefaultMessageCount { get; set; } = 100;
        public int MessagesPerPage { get; set; } = 100;
        public bool ShowDeadLetterBadges { get; set; } = true;
        public bool ShowTopicActionButtons { get; set; } = true;
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
        public string PinnedEntitiesJson { get; set; } = "[]";
        public bool HasSeenIntroduction { get; set; }
        public bool EnableTelemetry { get; set; }
        public bool AutoCheckForUpdates { get; set; } = true;
        public string? SkippedUpdateVersion { get; set; }
        public DateTime? UpdateRemindLaterDate { get; set; }
        public int SaveCount { get; private set; }

        public event EventHandler? PreferencesChanged
        {
            add { }
            remove { }
        }

        public void Save()
        {
            SaveCount++;
        }

        public void Load()
        {
        }
    }
}
