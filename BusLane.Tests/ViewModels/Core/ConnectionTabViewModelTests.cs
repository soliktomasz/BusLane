namespace BusLane.Tests.ViewModels.Core;

// BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
using Azure.Core;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;

public class ConnectionTabViewModelTests
{
    private static ILogSink CreateMockLogSink()
    {
        var logSink = Substitute.For<ILogSink>();
        logSink.GetLogs().Returns(new List<LogEntry>());
        return logSink;
    }

    [Fact]
    public void Constructor_SetsTabIdAndTitle()
    {
        // Arrange & Act
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net");

        // Assert
        tab.TabId.Should().Be("test-id");
        tab.TabTitle.Should().Be("Test Tab");
        tab.TabSubtitle.Should().Be("test.servicebus.windows.net");
    }

    [Fact]
    public void Constructor_InitializesNavigationState()
    {
        // Arrange & Act
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net");

        // Assert
        tab.Navigation.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_SetsDefaultConnectionState()
    {
        // Arrange & Act
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net");

        // Assert
        tab.IsConnected.Should().BeFalse();
        tab.IsLoading.Should().BeFalse();
        tab.Mode.Should().Be(ConnectionMode.None);
    }

    [Fact]
    public void Constructor_DefaultsEntityPaneToVisible()
    {
        // Arrange & Act
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net");

        // Assert
        tab.IsEntityPaneVisible.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithPreferencesService_InitializesMessageOps()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();

        // Act
        var tab = new ConnectionTabViewModel(
            "test-id",
            "Test Tab",
            "test.servicebus.windows.net",
            preferencesService,
            logSink);

        // Assert
        tab.MessageOps.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithPreferencesService_InitializesSessionInspector()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();

        // Act
        var tab = new ConnectionTabViewModel(
            "test-id",
            "Test Tab",
            "test.servicebus.windows.net",
            preferencesService,
            logSink);

        // Assert
        tab.SessionInspector.Should().NotBeNull();
    }

    [Fact]
    public async Task ConnectWithConnectionStringAsync_SetsConnectionState()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());

        var connection = new SavedConnection
        {
            Id = "conn-1",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            Type = ConnectionType.Namespace
        };

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);

        // Act
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

        // Assert
        tab.IsConnected.Should().BeTrue();
        tab.Mode.Should().Be(ConnectionMode.ConnectionString);
        tab.SavedConnection.Should().Be(connection);
    }

    [Fact]
    public async Task DisconnectAsync_ClearsConnectionState()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());

        var connection = new SavedConnection
        {
            Id = "conn-1",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            Type = ConnectionType.Namespace
        };

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

        // Act
        await tab.DisconnectAsync();

        // Assert
        tab.IsConnected.Should().BeFalse();
        tab.Mode.Should().Be(ConnectionMode.None);
    }

    [Fact]
    public async Task DisconnectAsync_ClearsSessionInspectorState()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        preferencesService.MessagesPerPage.Returns(25);
        preferencesService.MaxTotalMessages.Returns(500);
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());
        operations.GetSessionInspectorItemsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new SessionInspectorItem(
                    "session-a",
                    ActiveMessageCount: 3,
                    DeadLetterMessageCount: 0,
                    LastActivityAt: DateTimeOffset.UtcNow,
                    LockedUntil: null,
                    State: null)
            });

        var connection = new SavedConnection
        {
            Id = "conn-1",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            Type = ConnectionType.Namespace
        };

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);
        tab.Navigation.SelectedQueue = new QueueInfo(
            "orders",
            MessageCount: 3,
            ActiveMessageCount: 3,
            DeadLetterCount: 0,
            ScheduledCount: 0,
            SizeInBytes: 0,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: true,
            DefaultMessageTtl: TimeSpan.FromDays(14),
            LockDuration: TimeSpan.FromMinutes(1));
        await tab.SessionInspector.LoadSessionsAsync();

        // Act
        await tab.DisconnectAsync();

        // Assert
        tab.SessionInspector.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task ConnectWithConnectionStringAsync_EmitsActivityLogs()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());

        var connection = new SavedConnection
        {
            Id = "conn-1",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            Type = ConnectionType.Namespace
        };

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);

        // Act
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

        // Assert
        logSink.Received().Log(Arg.Is<LogEntry>(entry =>
            entry.Level == LogLevel.Info &&
            entry.Message.Contains("connecting with saved connection", StringComparison.OrdinalIgnoreCase)));

        logSink.Received().Log(Arg.Is<LogEntry>(entry =>
            entry.Level == LogLevel.Info &&
            entry.Message.Contains("connection established", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task DisconnectAsync_EmitsActivityLog()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());

        var connection = new SavedConnection
        {
            Id = "conn-1",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            Type = ConnectionType.Namespace
        };

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

        // Act
        await tab.DisconnectAsync();

        // Assert
        logSink.Received().Log(Arg.Is<LogEntry>(entry =>
            entry.Level == LogLevel.Info &&
            entry.Message.Contains("disconnected", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ConnectWithAzureCredentialAsync_SetsSelectedNamespaceInNavigation()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IAzureCredentialOperations>();

        operationsFactory.CreateFromAzureCredential(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TokenCredential>())
            .Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());

        var ns = new ServiceBusNamespace(
            "/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns-1",
            "ns-1",
            "rg",
            "sub-1",
            "westeurope",
            "ns-1.servicebus.windows.net");

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);

        // Act
        await tab.ConnectWithAzureCredentialAsync(ns, new TestTokenCredential(), operationsFactory);

        // Assert
        tab.Namespace.Should().Be(ns);
        tab.Navigation.SelectedNamespace.Should().Be(ns);
    }

    [Fact]
    public void ApplySessionState_WithHiddenEntityPane_RestoresVisibility()
    {
        // Arrange
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net");
        var state = new TabSessionState
        {
            TabId = "test-id",
            Mode = ConnectionMode.ConnectionString,
            ConnectionId = "conn-1",
            IsEntityPaneVisible = false,
            TabOrder = 0
        };

        // Act
        tab.ApplySessionState(state);

        // Assert
        tab.IsEntityPaneVisible.Should().BeFalse();
    }

    [Fact]
    public void CreateSessionState_IncludesEntityPaneVisibility()
    {
        // Arrange
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net")
        {
            IsEntityPaneVisible = false
        };

        // Act
        var state = tab.CreateSessionState(2);

        // Assert
        state.IsEntityPaneVisible.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshNamespaceEntitiesAsync_KeepsSelectedNamespace()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IAzureCredentialOperations>();

        operationsFactory.CreateFromAzureCredential(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TokenCredential>())
            .Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());

        var ns = new ServiceBusNamespace(
            "/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns-1",
            "ns-1",
            "rg",
            "sub-1",
            "westeurope",
            "ns-1.servicebus.windows.net");

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);
        await tab.ConnectWithAzureCredentialAsync(ns, new TestTokenCredential(), operationsFactory);

        // Act
        await tab.RefreshNamespaceEntitiesAsync();

        // Assert
        tab.Navigation.SelectedNamespace.Should().Be(ns);
    }

    [Fact]
    public async Task ProbeConnectionHealthAsync_StoresExplicitHealthState()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());
        operations.CheckConnectionHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new ConnectionHealthReport(ConnectionHealthState.Throttled, "Server busy", "Retry later"));

        var connection = new SavedConnection
        {
            Id = "conn-1",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            Type = ConnectionType.Namespace
        };

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

        // Act
        var report = await tab.ProbeConnectionHealthAsync();

        // Assert
        report.State.Should().Be(ConnectionHealthState.Throttled);
        tab.ConnectionHealth.State.Should().Be(ConnectionHealthState.Throttled);
        tab.StatusMessage.Should().Contain("Server busy");
    }

    [Fact]
    public async Task ProbeConnectionHealthAsync_UpdatesStatusMessageForHealthyReport()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());
        operations.CheckConnectionHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new ConnectionHealthReport(ConnectionHealthState.Healthy, "Health probe succeeded"));

        var connection = new SavedConnection
        {
            Id = "conn-1",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            Type = ConnectionType.Namespace
        };

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

        // Act
        var report = await tab.ProbeConnectionHealthAsync();

        // Assert
        report.State.Should().Be(ConnectionHealthState.Healthy);
        tab.ConnectionHealth.Should().Be(report);
        tab.StatusMessage.Should().Be("Health probe succeeded");
    }

    [Fact]
    public async Task ProbeConnectionHealthAsync_ConvertsHealthCheckExceptionsToDegradedReport()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();

        operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        operations.GetQueuesAsync().Returns(new List<QueueInfo>());
        operations.GetTopicsAsync().Returns(new List<TopicInfo>());
        operations.CheckConnectionHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ConnectionHealthReport>(new InvalidOperationException("Health probe failed")));

        var connection = new SavedConnection
        {
            Id = "conn-1",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
            Type = ConnectionType.Namespace
        };

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink);
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

        // Act
        var report = await tab.ProbeConnectionHealthAsync();

        // Assert
        report.State.Should().Be(ConnectionHealthState.Degraded);
        report.Summary.Should().Be("Health probe failed");
        tab.ConnectionHealth.Should().Be(report);
        tab.StatusMessage.Should().Be("Health probe failed");
    }

    [Fact]
    public void CreateSessionState_CapturesEntityAndFilterContext()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink)
        {
            Mode = ConnectionMode.ConnectionString
        };

        tab.Navigation.EntityFilter = "orders";
        tab.Navigation.ShowDeadLetter = true;
        tab.Navigation.SelectedMessageTabIndex = 1;
        tab.MessageOps.MessageSearchText = "order-42";
        tab.Navigation.SelectedQueue = new QueueInfo(
            "orders",
            MessageCount: 1,
            ActiveMessageCount: 1,
            DeadLetterCount: 0,
            ScheduledCount: 0,
            SizeInBytes: 0,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: false,
            DefaultMessageTtl: TimeSpan.FromDays(14),
            LockDuration: TimeSpan.FromMinutes(1));

        // Act
        var state = tab.CreateSessionState(0);

        // Assert
        state.SelectedEntityName.Should().Be("orders");
        state.EntityFilter.Should().Be("orders");
        state.ShowDeadLetter.Should().BeTrue();
        state.SelectedMessageTabIndex.Should().Be(1);
        state.MessageSearchText.Should().Be("order-42");
    }

    [Fact]
    public void CreateSessionState_WithSelectedSubscription_SerializesTopicAndSubscriptionName()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
        var logSink = CreateMockLogSink();
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService, logSink)
        {
            Mode = ConnectionMode.ConnectionString
        };

        tab.Navigation.SelectedSubscription = new SubscriptionInfo(
            Name: "processor",
            TopicName: "orders",
            MessageCount: 1,
            ActiveMessageCount: 1,
            DeadLetterCount: 0,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: false);

        // Act
        var state = tab.CreateSessionState(0);

        // Assert
        state.SelectedEntityName.Should().Be("orders/processor");
    }

    private sealed class TestTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1)));
    }
}
