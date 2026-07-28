namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class CorrelationExplorerViewModelTests
{
    [Fact]
    public async Task RefreshAsync_LoadsGroupsTimelineAndAuditHistory()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("second", 2));
        catalog.Add(CreateMessage("first", 1));
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([
            CreateAuditEntry()
        ]);
        var sut = CreateSut(catalog, auditStore);

        // Act
        await sut.RefreshAsync();

        // Assert
        sut.Groups.Should().ContainSingle();
        sut.SelectedGroup.Should().NotBeNull();
        sut.Timeline.Select(static message => message.MessageId).Should().ContainInOrder("first", "second");
        sut.ReplayHistory.Should().ContainSingle();
    }

    [Fact]
    public async Task FilterText_LimitsCorrelationGroups()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("message-1", 1, "corr-orders"));
        catalog.Add(CreateMessage("message-2", 2, "corr-billing"));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();

        // Act
        sut.FilterText = "corr-orders";

        // Assert
        sut.Groups.Should().ContainSingle().Which.DisplayId.Should().Be("corr-orders");
    }

    [Fact]
    public async Task OpenReplay_WithSelectedMessage_CreatesReplayEditor()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        catalog.Add(CreateMessage("message-1", 1));
        var sut = CreateSut(catalog, Substitute.For<IReplayAuditStore>());
        await sut.RefreshAsync();

        // Act
        sut.OpenReplayCommand.Execute(null);

        // Assert
        sut.ReplayEditor.Should().NotBeNull();
        sut.ShowReplayEditor.Should().BeTrue();
    }

    [Fact]
    public async Task ExportHistoryAsync_WritesJsonToSelectedPath()
    {
        // Arrange
        var catalog = new CorrelationMessageCatalog();
        var auditStore = Substitute.For<IReplayAuditStore>();
        auditStore.LoadAsync(Arg.Any<CancellationToken>()).Returns([CreateAuditEntry()]);
        var path = Path.Combine(Path.GetTempPath(), $"replay-history-{Guid.NewGuid():N}.json");
        var fileDialog = Substitute.For<IFileDialogService>();
        fileDialog.SaveFileAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>())
            .Returns(path);
        var sut = CreateSut(catalog, auditStore, fileDialog);
        await sut.RefreshAsync();

        try
        {
            // Act
            await sut.ExportHistoryCommand.ExecuteAsync(null);

            // Assert
            File.Exists(path).Should().BeTrue();
            (await File.ReadAllTextAsync(path)).Should().Contain("Succeeded");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static CorrelationExplorerViewModel CreateSut(
        ICorrelationMessageCatalog catalog,
        IReplayAuditStore auditStore,
        IFileDialogService? fileDialog = null)
    {
        var replayService = Substitute.For<IMessageReplayService>();
        var destination = new ReplayDestination(
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders-replay",
            "Queue",
            false);
        replayService.CreateRequest(Arg.Any<CorrelationMessage>(), destination)
            .Returns(call =>
            {
                var source = call.Arg<CorrelationMessage>()!;
                return new ReplayRequest
                {
                    Source = source,
                    Destination = destination,
                    Body = source.Body,
                    MessageId = "new-id"
                };
            });

        return new CorrelationExplorerViewModel(
            catalog,
            auditStore,
            replayService,
            () => Substitute.For<IServiceBusOperations>(),
            () => [destination],
            fileDialog);
    }

    private static CorrelationMessage CreateMessage(
        string messageId,
        long sequenceNumber,
        string correlationId = "corr-1") =>
        new(
            CorrelationMessageSource.Loaded,
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            messageId,
            correlationId,
            null,
            "application/json",
            "{}",
            DateTimeOffset.Parse("2026-07-28T09:00:00Z").AddMinutes(sequenceNumber),
            sequenceNumber,
            new Dictionary<string, object>());

    private static ReplayAuditEntry CreateAuditEntry() =>
        new(
            "audit-1",
            DateTimeOffset.Parse("2026-07-28T10:00:00Z"),
            ReplayAuditOutcome.Succeeded,
            "message-1",
            "corr-1",
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders-replay",
            false,
            1,
            ["MessageId"],
            [],
            "Message replayed successfully");
}
