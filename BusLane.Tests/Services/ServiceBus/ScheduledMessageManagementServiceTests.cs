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

    [Fact]
    public async Task CancelAsync_ProductionWithoutAcknowledgement_DoesNotCallBroker()
    {
        var (sut, operations, _) = CreateSut();
        var entry = CreateEntry() with { Environment = ConnectionEnvironment.Production };

        var result = await sut.CancelAsync(new ScheduledMessageActionRequest(entry, true, false));

        result.IsSuccess.Should().BeFalse();
        await operations.DidNotReceiveWithAnyArgs().CancelScheduledMessageAsync(default!, default);
    }

    [Fact]
    public async Task RefreshAsync_MissingConnection_ReturnsStaleState()
    {
        var (sut, _, store) = CreateSut();
        var entry = CreateEntry() with { ConnectionId = "missing" };
        store.LoadAsync(Arg.Any<CancellationToken>()).Returns([entry]);

        var result = await sut.RefreshAsync();

        result.Should().ContainSingle(item => item.IsStale && item.LocalState == "Limited / stale");
    }

    [Fact]
    public async Task RescheduleAsync_WhenReplacementFails_PreservesConfirmedCancellation()
    {
        var (sut, operations, store) = CreateSut();
        store.LoadPayloadAsync(Arg.Any<ScheduledMessageIndexEntry>(), Arg.Any<CancellationToken>())
            .Returns(new ScheduledMessagePayload(
                "body", null, null, null, null, null, null, null, null, null, null,
                new Dictionary<string, ScheduledMessagePropertyValue>()));
        operations.ScheduleMessageAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDictionary<string, object>>(),
                Arg.Any<DateTimeOffset>(), ct: Arg.Any<CancellationToken>())
            .Returns<long>(_ => throw new InvalidOperationException("schedule failed"));

        var result = await sut.RescheduleAsync(new ScheduledMessageActionRequest(
            CreateEntry(), true, true, DateTimeOffset.UtcNow.AddHours(2)));

        result.IsPartialFailure.Should().BeTrue();
        result.Entry.Status.Should().Be(ScheduledMessageRecordStatus.Cancelled);
    }

    [Fact]
    public async Task RescheduleAsync_Success_CancelsThenSchedulesAndLinksReplacement()
    {
        var (sut, operations, store) = CreateSut();
        store.LoadPayloadAsync(Arg.Any<ScheduledMessageIndexEntry>(), Arg.Any<CancellationToken>())
            .Returns(new ScheduledMessagePayload(
                "body", null, null, null, null, null, null, null, null, null, null,
                new Dictionary<string, ScheduledMessagePropertyValue>()));
        operations.ScheduleMessageAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDictionary<string, object>>(),
                Arg.Any<DateTimeOffset>(), ct: Arg.Any<CancellationToken>())
            .Returns(99);

        var result = await sut.RescheduleAsync(new ScheduledMessageActionRequest(
            CreateEntry(), true, true, DateTimeOffset.UtcNow.AddHours(2)));

        result.IsSuccess.Should().BeTrue();
        result.Entry.Status.Should().Be(ScheduledMessageRecordStatus.Rescheduled);
        result.Entry.ReplacementRecordId.Should().NotBeNullOrWhiteSpace();
        await store.Received(1).AddAsync(
            Arg.Is<ScheduledMessageIndexEntry>(entry => entry.SequenceNumber == 99),
            Arg.Any<ScheduledMessagePayload>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_MarksOnlyLocalResolution()
    {
        var (sut, operations, store) = CreateSut();

        await sut.ResolveLocallyAsync(CreateEntry());

        await operations.DidNotReceiveWithAnyArgs().CancelScheduledMessageAsync(default!, default);
        await store.Received(1).UpdateAsync(
            Arg.Is<ScheduledMessageIndexEntry>(entry =>
                entry.Status == ScheduledMessageRecordStatus.ResolvedLocally &&
                !entry.IsBrokerConfirmed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_SavedConnectionRepointedToDifferentNamespace_DoesNotCallBroker()
    {
        var (sut, operations, _) = CreateSut();
        var entry = CreateEntry() with { NamespaceEndpoint = "original.servicebus.windows.net" };

        var result = await sut.CancelAsync(new ScheduledMessageActionRequest(entry, true, true));

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("no longer matches");
        await operations.DidNotReceiveWithAnyArgs().CancelScheduledMessageAsync(default!, default);
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
