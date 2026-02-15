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

    private sealed class TestTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1)));
    }
}
