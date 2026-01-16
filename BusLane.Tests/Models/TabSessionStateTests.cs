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
}
