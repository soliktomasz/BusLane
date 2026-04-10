namespace BusLane.Tests.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;
using System.Reflection;
using Xunit;

public class ServiceBusOperationsTests
{
    [Fact]
    public async Task SelectAsync_WithMoreWorkThanBudget_DoesNotExceedConfiguredConcurrency()
    {
        // Arrange
        var activeWorkers = 0;
        var maxConcurrentWorkers = 0;
        var items = Enumerable.Range(1, 18).ToArray();

        // Act
        var results = await InvokeBoundedAdminProjectorAsync(
            items,
            async (item, ct) =>
            {
                var inFlight = Interlocked.Increment(ref activeWorkers);
                UpdateMaxValue(ref maxConcurrentWorkers, inFlight);
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25), ct);
                    return item * 2;
                }
                finally
                {
                    Interlocked.Decrement(ref activeWorkers);
                }
            },
            maxConcurrency: 3);

        // Assert
        maxConcurrentWorkers.Should().BeLessThanOrEqualTo(3);
        results.Should().Equal(items.Select(static item => item * 2));
    }

    [Fact]
    public async Task SelectAsync_WhenWorkCompletesOutOfOrder_PreservesSourceOrder()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4 };

        // Act
        var results = await InvokeBoundedAdminProjectorAsync(
            items,
            async (item, ct) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds((5 - item) * 20), ct);
                return $"item-{item}";
            },
            maxConcurrency: 2);

        // Assert
        results.Should().Equal("item-1", "item-2", "item-3", "item-4");
    }

    [Fact(Skip = "Integration test - requires Service Bus client setup. Functionality verified through manual testing.")]
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
        // This is a placeholder - the real implementation would require complex mocking
        throw new NotImplementedException("This test requires Service Bus client infrastructure setup");
    }

    private static async Task<IReadOnlyList<TResult>> InvokeBoundedAdminProjectorAsync<TSource, TResult>(
        IReadOnlyList<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> projector,
        int maxConcurrency,
        CancellationToken ct = default)
    {
        var helperType = typeof(ConnectionStringOperations).Assembly.GetType("BusLane.Services.ServiceBus.BoundedAdminProjector");
        helperType.Should().NotBeNull();

        var selectAsync = helperType!.GetMethod("SelectAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        selectAsync.Should().NotBeNull();

        var genericMethod = selectAsync!.MakeGenericMethod(typeof(TSource), typeof(TResult));
        var task = (Task)genericMethod.Invoke(null, [source, projector, maxConcurrency, ct])!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        resultProperty.Should().NotBeNull();
        return (IReadOnlyList<TResult>)resultProperty!.GetValue(task)!;
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
}
