// BusLane.Tests/Models/TabSessionStateTests.cs
using BusLane.Models;
using BusLane.ViewModels;
using FluentAssertions;

namespace BusLane.Tests.Models;

public class TabSessionStateTests
{
    [Fact]
    public void Create_WithConnectionStringMode_SetsCorrectProperties()
    {
        // Arrange & Act
        var state = new TabSessionState
        {
            TabId = "tab-1",
            Mode = ConnectionMode.ConnectionString,
            ConnectionId = "conn-1",
            TabOrder = 0
        };

        // Assert
        state.TabId.Should().Be("tab-1");
        state.Mode.Should().Be(ConnectionMode.ConnectionString);
        state.ConnectionId.Should().Be("conn-1");
        state.NamespaceId.Should().BeNull();
    }

    [Fact]
    public void Create_WithAzureMode_SetsCorrectProperties()
    {
        // Arrange & Act
        var state = new TabSessionState
        {
            TabId = "tab-2",
            Mode = ConnectionMode.AzureAccount,
            NamespaceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns",
            TabOrder = 1
        };

        // Assert
        state.Mode.Should().Be(ConnectionMode.AzureAccount);
        state.NamespaceId.Should().NotBeNullOrEmpty();
        state.ConnectionId.Should().BeNull();
    }

    [Fact]
    public void Create_WithNavigationContext_PreservesFiltersAndDeadLetterMode()
    {
        var state = new TabSessionState
        {
            TabId = "tab-3",
            Mode = ConnectionMode.ConnectionString,
            ConnectionId = "conn-3",
            SelectedEntityName = "orders",
            EntityFilter = "ord",
            MessageSearchText = "customer-42",
            ShowDeadLetter = true,
            SelectedMessageTabIndex = 1,
            TabOrder = 2
        };

        state.EntityFilter.Should().Be("ord");
        state.MessageSearchText.Should().Be("customer-42");
        state.ShowDeadLetter.Should().BeTrue();
        state.SelectedMessageTabIndex.Should().Be(1);
    }

    [Fact]
    public void Create_WithHiddenEntityPane_PreservesVisibilityState()
    {
        // Arrange & Act
        var state = new TabSessionState
        {
            TabId = "tab-4",
            Mode = ConnectionMode.ConnectionString,
            ConnectionId = "conn-4",
            IsEntityPaneVisible = false,
            TabOrder = 3
        };

        // Assert
        state.IsEntityPaneVisible.Should().BeFalse();
    }
}
