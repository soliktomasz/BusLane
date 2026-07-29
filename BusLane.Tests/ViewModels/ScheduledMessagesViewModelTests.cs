namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class ScheduledMessagesViewModelTests
{
    [Fact]
    public async Task RefreshAsync_LoadsResolvedEntriesAndDefaultsToUpcoming()
    {
        var service = Substitute.For<IScheduledMessageManagementService>();
        service.RefreshAsync(Arg.Any<CancellationToken>()).Returns(
            [new ScheduledMessageResolvedEntry(CreateEntry(), "Upcoming (local)", false)]);
        var sut = new ScheduledMessagesViewModel(service, (_, _) => Task.CompletedTask, TimeProvider.System);

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.FilteredEntries.Should().ContainSingle();
        sut.SelectedStatus.Should().Be("Upcoming");
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("message-42")]
    [InlineData("corr-42")]
    [InlineData("tenant")]
    [InlineData("north")]
    public async Task SearchText_MatchesSupportedMetadata(string searchText)
    {
        var service = Substitute.For<IScheduledMessageManagementService>();
        service.RefreshAsync(Arg.Any<CancellationToken>()).Returns(
            [new ScheduledMessageResolvedEntry(CreateEntry(), "Upcoming (local)", false)]);
        service.LoadPayloadAsync(Arg.Any<ScheduledMessageIndexEntry>(), Arg.Any<CancellationToken>())
            .Returns(new ScheduledMessagePayload(
                "order body", null, "corr-42", "message-42", null, null, null, null, null,
                null, null,
                new Dictionary<string, ScheduledMessagePropertyValue>
                {
                    ["tenant"] = new("String", "north")
                }));
        var sut = new ScheduledMessagesViewModel(service, (_, _) => Task.CompletedTask, TimeProvider.System);
        await sut.RefreshCommand.ExecuteAsync(null);

        sut.SearchText = searchText;

        sut.FilteredEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task CalendarDays_ProjectTheSameFilteredEntries()
    {
        var service = Substitute.For<IScheduledMessageManagementService>();
        service.RefreshAsync(Arg.Any<CancellationToken>()).Returns(
            [new ScheduledMessageResolvedEntry(CreateEntry(), "Upcoming (local)", false)]);
        var sut = new ScheduledMessagesViewModel(service, (_, _) => Task.CompletedTask, TimeProvider.System);
        await sut.RefreshCommand.ExecuteAsync(null);
        sut.SelectedMonth = DateTimeOffset.UtcNow;

        sut.CalendarDays.SelectMany(day => day.Entries).Should().Equal(sut.FilteredEntries);
    }

    [Fact]
    public async Task BeginCancel_ResetsProductionAcknowledgement()
    {
        var service = Substitute.For<IScheduledMessageManagementService>();
        var sut = new ScheduledMessagesViewModel(service, (_, _) => Task.CompletedTask, TimeProvider.System)
        {
            IsProductionAcknowledged = true
        };

        sut.BeginCancelCommand.Execute(new ScheduledMessageResolvedEntry(
            CreateEntry() with { Environment = ConnectionEnvironment.Production },
            "Upcoming (local)", false));

        sut.IsProductionAcknowledged.Should().BeFalse();
    }

    [Fact]
    public async Task RescheduleAsync_PastTime_ShowsValidationError()
    {
        var service = Substitute.For<IScheduledMessageManagementService>();
        var sut = new ScheduledMessagesViewModel(service, (_, _) => Task.CompletedTask, TimeProvider.System);
        var item = new ScheduledMessageResolvedEntry(CreateEntry(), "Upcoming (local)", false);
        sut.BeginRescheduleCommand.Execute(item);
        sut.RescheduleTime = DateTimeOffset.UtcNow.AddDays(-1);
        sut.RescheduleClockTime = TimeSpan.Zero;

        await sut.ConfirmActionCommand.ExecuteAsync(null);

        sut.StatusText.Should().Be("A future scheduled time is required.");
        await service.DidNotReceiveWithAnyArgs().RescheduleAsync(default!, default);
    }

    [Fact]
    public async Task CloneAsync_LegacyEntry_ShowsPayloadUnavailable()
    {
        var service = Substitute.For<IScheduledMessageManagementService>();
        var legacy = CreateEntry() with { SchemaVersion = 1, EncryptedPayload = null };
        var sut = new ScheduledMessagesViewModel(service, (_, _) => Task.CompletedTask, TimeProvider.System);

        await sut.CloneCommand.ExecuteAsync(new ScheduledMessageResolvedEntry(legacy, "Limited / stale", true));

        sut.StatusText.Should().Be("The scheduled payload is unavailable.");
    }

    private static ScheduledMessageIndexEntry CreateEntry() => new()
    {
        RecordId = "record-1",
        ConnectionId = "connection-1",
        ConnectionName = "Development",
        EntityName = "orders",
        MessageId = "message-42",
        CorrelationId = "corr-42",
        SequenceNumber = 42,
        ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddHours(1),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        EncryptedPayload = "encrypted"
    };
}
