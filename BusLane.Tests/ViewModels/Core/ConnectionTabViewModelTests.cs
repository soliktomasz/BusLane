// BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
using BusLane.ViewModels;
using BusLane.ViewModels.Core;
using FluentAssertions;

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
}
