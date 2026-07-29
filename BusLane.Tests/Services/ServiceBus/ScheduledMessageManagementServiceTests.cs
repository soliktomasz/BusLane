namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.Auth;
using BusLane.Services.ServiceBus;
using BusLane.Services.Storage;
using FluentAssertions;
using NSubstitute;

public class ScheduledMessageManagementServiceTests
{
    [Fact]
    public async Task CancelAsync_WithoutConfirmation_DoesNotCallBroker()
    {
        var (sut, operations, _) = CreateSut();

        var result = await sut.CancelAsync(new ScheduledMessageActionRequest(CreateEntry(), false, false));

        result.IsSuccess.Should().BeFalse();
        await operations.DidNotReceiveWithAnyArgs().CancelScheduledMessageAsync(default!, default);
    }

    [Fact]
    public async Task CancelAsync_Success_MarksBrokerConfirmedCancellation()
    {
        var (sut, operations, store) = CreateSut();

        var result = await sut.CancelAsync(new ScheduledMessageActionRequest(CreateEntry(), true, true));

        result.IsSuccess.Should().BeTrue();
        result.Entry.Status.Should().Be(ScheduledMessageRecordStatus.Cancelled);
        result.Entry.IsBrokerConfirmed.Should().BeTrue();
        await operations.Received(1).CancelScheduledMessageAsync("orders", 42, Arg.Any<CancellationToken>());
        await store.Received(1).UpdateAsync(
            Arg.Is<ScheduledMessageIndexEntry>(entry => entry.Status == ScheduledMessageRecordStatus.Cancelled),
            Arg.Any<CancellationToken>());
    }

    private static (ScheduledMessageManagementService Sut, IConnectionStringOperations Operations, IScheduledMessageStore Store)
        CreateSut()
    {
        var storage = Substitute.For<IConnectionStorageService>();
        storage.GetConnectionAsync("connection-1").Returns(new SavedConnection
        {
            Id = "connection-1",
            Name = "Development",
            ConnectionString = "Endpoint=sb://dev.servicebus.windows.net/;SharedAccessKeyName=x;SharedAccessKey=y",
            Environment = ConnectionEnvironment.Development
        });
        var factory = Substitute.For<IServiceBusOperationsFactory>();
        var operations = Substitute.For<IConnectionStringOperations>();
        factory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
        var store = Substitute.For<IScheduledMessageStore>();
        var auth = Substitute.For<IAzureAuthService>();
        return (new ScheduledMessageManagementService(storage, factory, auth, store, TimeProvider.System), operations, store);
    }

    private static ScheduledMessageIndexEntry CreateEntry() => new()
    {
        RecordId = "record-1",
        ConnectionId = "connection-1",
        ConnectionKind = ScheduledMessageConnectionKind.ConnectionString,
        Environment = ConnectionEnvironment.Development,
        EntityName = "orders",
        SequenceNumber = 42,
        ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddHours(1),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        EncryptedPayload = "encrypted"
    };
}
