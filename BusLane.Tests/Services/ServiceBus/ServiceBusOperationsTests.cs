using Azure.Messaging.ServiceBus;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BusLane.Tests.Services.ServiceBus;

public class ServiceBusOperationsTests
{
    [Fact]
    public async Task PeekMessagesAsync_WithSequenceNumber_ShouldCallPeekWithSequenceNumber()
    {
        // Arrange
        var receiver = Substitute.For<ServiceBusReceiver>();
        receiver.PeekMessagesAsync(50, 12345, Arg.Any<CancellationToken>())
                .Returns(new List<ServiceBusReceivedMessage>().AsReadOnly());
        
        var sut = CreateOperations(receiver);
        
        // Act
        await sut.PeekMessagesAsync("queue", null, 50, 12345, false, false);
        
        // Assert
        await receiver.Received(1).PeekMessagesAsync(50, 12345, Arg.Any<CancellationToken>());
    }
    
    private IServiceBusOperations CreateOperations(ServiceBusReceiver receiver)
    {
        // This is a placeholder - the real implementation will be in Task 3
        throw new NotImplementedException("CreateOperations needs to be implemented");
    }
}
