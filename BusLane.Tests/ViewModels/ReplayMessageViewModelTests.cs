namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class ReplayMessageViewModelTests
{
    [Fact]
    public void BuildPreview_PrefillsSourceAndShowsChangedMessageId()
    {
        // Arrange
        var replayService = Substitute.For<IMessageReplayService>();
        var source = CreateSource();
        var destination = CreateDestination();
        var request = CreateRequest(source, destination);
        replayService.CreateRequest(source, destination).Returns(request);
        replayService.Preview(Arg.Any<ReplayRequest>()).Returns(call =>
        {
            var value = call.Arg<ReplayRequest>()!;
            return new ReplayPreview(
                [],
                [],
                [new ReplayFieldChange("MessageId", source.MessageId, value.MessageId)]);
        });
        var sut = new ReplayMessageViewModel(
            source,
            [destination],
            replayService,
            () => Substitute.For<IServiceBusOperations>());

        // Act
        sut.BuildPreviewCommand.Execute(null);

        // Assert
        sut.Body.Should().Be(source.Body);
        sut.MessageId.Should().Be("new-message-id");
        sut.Preview.Should().NotBeNull();
        sut.Preview!.Changes.Should().ContainSingle(change => change.Field == "MessageId");
        sut.HasPreview.Should().BeTrue();
    }

    [Fact]
    public async Task ReplayAsync_AfterPreview_PassesConfirmationAndEditedProperties()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var replayService = Substitute.For<IMessageReplayService>();
        var source = CreateSource();
        var destination = CreateDestination();
        var initial = CreateRequest(source, destination);
        replayService.CreateRequest(source, destination).Returns(initial);
        replayService.Preview(Arg.Any<ReplayRequest>()).Returns(new ReplayPreview([], [], []));
        replayService.ReplayAsync(
                operations,
                Arg.Any<ReplayRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new ReplayResult(true, false, "Message replayed successfully"));
        var sut = new ReplayMessageViewModel(source, [destination], replayService, () => operations);
        sut.CustomProperties.Single().Value = "south";
        sut.IsConfirmed = true;
        sut.BuildPreviewCommand.Execute(null);

        // Act
        await sut.ReplayCommand.ExecuteAsync(null);

        // Assert
        sut.ResultMessage.Should().Be("Message replayed successfully");
        await replayService.Received(1).ReplayAsync(
            operations,
            Arg.Is<ReplayRequest>(request =>
                request != null &&
                request.IsConfirmed &&
                request.Properties["tenant"].Equals("south")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayAsync_WithoutPreview_ShowsErrorAndDoesNotReplay()
    {
        // Arrange
        var replayService = Substitute.For<IMessageReplayService>();
        var source = CreateSource();
        var destination = CreateDestination();
        replayService.CreateRequest(source, destination).Returns(CreateRequest(source, destination));
        var sut = new ReplayMessageViewModel(
            source,
            [destination],
            replayService,
            () => Substitute.For<IServiceBusOperations>());

        // Act
        await sut.ReplayCommand.ExecuteAsync(null);

        // Assert
        sut.ErrorMessage.Should().Be("Preview the replay request before sending");
        await replayService.DidNotReceiveWithAnyArgs().ReplayAsync(default!, default!, default);
    }

    private static ReplayRequest CreateRequest(CorrelationMessage source, ReplayDestination destination)
    {
        return new ReplayRequest
        {
            Source = source,
            Destination = destination,
            Body = source.Body,
            ContentType = source.ContentType,
            CorrelationId = source.CorrelationId,
            MessageId = "new-message-id",
            SessionId = source.SessionId,
            Subject = source.Subject,
            Properties = source.Properties
        };
    }

    private static ReplayDestination CreateDestination() =>
        new("demo.servicebus.windows.net", ConnectionEnvironment.Test, "orders-replay", "Queue", false);

    private static CorrelationMessage CreateSource() =>
        new(
            CorrelationMessageSource.Loaded,
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            "message-1",
            "corr-1",
            "session-1",
            "application/json",
            "{}",
            DateTimeOffset.Parse("2026-07-28T09:00:00Z"),
            1,
            new Dictionary<string, object> { ["tenant"] = "north" },
            Subject: "created");
}
