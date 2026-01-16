// BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;

namespace BusLane.Tests.ViewModels.Core;

public class ConnectionTabViewModelTests
{
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

        // Act
        var tab = new ConnectionTabViewModel(
            "test-id",
            "Test Tab",
            "test.servicebus.windows.net",
            preferencesService);

        // Assert
        tab.MessageOps.Should().NotBeNull();
    }

    [Fact]
    public async Task ConnectWithConnectionStringAsync_SetsConnectionState()
    {
        // Arrange
        var preferencesService = Substitute.For<IPreferencesService>();
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

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService);

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

        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService);
        await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

        // Act
        await tab.DisconnectAsync();

        // Assert
        tab.IsConnected.Should().BeFalse();
        tab.Mode.Should().Be(ConnectionMode.None);
    }
}
