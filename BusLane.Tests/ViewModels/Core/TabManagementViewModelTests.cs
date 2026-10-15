namespace BusLane.Tests.ViewModels.Core;

using Azure.Core;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.Auth;
using BusLane.Services.ServiceBus;
using BusLane.Services.Storage;
using BusLane.ViewModels;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class TabManagementViewModelTests
{
    private const string NamespaceId =
        "/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/orders";

    [Fact]
    public async Task RestoreTabSessionAsync_WhenSignedIn_ReopensAzureTabsAndKeepsThemInSession()
    {
        // Arrange
        var preferences = Substitute.For<IPreferencesService>();
        preferences.RestoreTabsOnStartup.Returns(true);
        preferences.OpenTabsJson = Serialize(new List<TabSessionState>
        {
            new() { TabId = "tab-1", Mode = ConnectionMode.AzureAccount, NamespaceId = NamespaceId, TabOrder = 0 }
        });
        var auth = Substitute.For<IAzureAuthService>();
        auth.Credential.Returns(Substitute.For<TokenCredential>());
        var azureResources = Substitute.For<IAzureResourceService>();
        azureResources.GetNamespacesAsync("sub-1", Arg.Any<CancellationToken>()).Returns([
            new ServiceBusNamespace(NamespaceId, "orders", "rg", "sub-1", "westeurope", "orders.servicebus.windows.net")
        ]);
        var operations = Substitute.For<IAzureCredentialOperations>();
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<QueueInfo>());
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TopicInfo>());
        var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
        operationsFactory.CreateFromAzureCredential(Arg.Any<string>(), NamespaceId, Arg.Any<TokenCredential>())
            .Returns(operations);
        var sut = new TabManagementViewModel(
            operationsFactory,
            preferences,
            Substitute.For<IConnectionStorageService>(),
            auth,
            Substitute.For<ILogSink>(),
            _ => { },
            azureResources: azureResources);

        // Act
        await sut.RestoreTabSessionAsync();

        // Assert
        sut.ConnectionTabs.Should().ContainSingle(tab => tab.Mode == ConnectionMode.AzureAccount);
        DeserializeList<TabSessionState>(preferences.OpenTabsJson)
            .Should().ContainSingle(state => state.NamespaceId == NamespaceId);
    }
}
