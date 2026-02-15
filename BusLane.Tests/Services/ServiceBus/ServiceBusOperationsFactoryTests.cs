namespace BusLane.Tests.Services.ServiceBus;

using Azure.Core;
using Azure.ResourceManager;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;

public class ServiceBusOperationsFactoryTests
{
    [Fact]
    public void CreateFromConnectionString_WithValidConnectionString_ReturnsConnectionStringOperations()
    {
        // Arrange
        var factory = new ServiceBusOperationsFactory(() => null);
        const string connectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test123";

        // Act
        var operations = factory.CreateFromConnectionString(connectionString);

        // Assert
        operations.Should().NotBeNull();
        operations.Should().BeOfType<ConnectionStringOperations>();
    }

    [Fact]
    public void CreateFromConnectionString_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var factory = new ServiceBusOperationsFactory(() => null);

        // Act
        var act = () => factory.CreateFromConnectionString("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromConnectionString_WithWhitespace_ThrowsArgumentException()
    {
        // Arrange
        var factory = new ServiceBusOperationsFactory(() => null);

        // Act
        var act = () => factory.CreateFromConnectionString("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromAzureCredential_WhenArmClientIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new ServiceBusOperationsFactory(() => null);
        var credential = Substitute.For<TokenCredential>();

        // Act
        var act = () => factory.CreateFromAzureCredential(
            "test.servicebus.windows.net",
            "/subscriptions/sub-id/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/test",
            credential);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ArmClient*");
    }

    [Fact]
    public void CreateFromAzureCredential_WhenArmClientIsProvided_ReturnsAzureCredentialOperations()
    {
        // Arrange
        var armClient = Substitute.For<ArmClient>();
        var factory = new ServiceBusOperationsFactory(() => armClient);
        var credential = Substitute.For<TokenCredential>();

        // Act
        var operations = factory.CreateFromAzureCredential(
            "test.servicebus.windows.net",
            "/subscriptions/sub-id/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/test",
            credential);

        // Assert
        operations.Should().NotBeNull();
        operations.Should().BeOfType<AzureCredentialOperations>();
    }

    [Fact]
    public void Constructor_WithGetArmClientFunc_StoresFunc()
    {
        // Arrange
        var armClient = Substitute.For<ArmClient>();
        var armClientFunc = () => armClient;

        // Act
        var factory = new ServiceBusOperationsFactory(armClientFunc);
        var credential = Substitute.For<TokenCredential>();

        // Call to verify the func is used
        var operations = factory.CreateFromAzureCredential(
            "test.servicebus.windows.net",
            "/subscriptions/sub-id/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/test",
            credential);

        // Assert
        operations.Should().NotBeNull();
    }
}
