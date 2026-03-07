namespace BusLane.Tests.ViewModels.Core;

using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;

public class MessageOperationsViewModelTests
{
    [Fact]
    public async Task LoadMessagesAsync_WhenSessionScopeIsActive_PassesSessionIdToOperations()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.PeekMessagesAsync(
                "orders",
                null,
                Arg.Any<int>(),
                null,
                false,
                true,
                "session-a",
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MessageInfo>());

        var preferences = Substitute.For<IPreferencesService>();
        preferences.MessagesPerPage.Returns(25);
        preferences.MaxTotalMessages.Returns(500);

        var logSink = Substitute.For<ILogSink>();
        var sut = new MessageOperationsViewModel(
            () => operations,
            preferences,
            logSink,
            () => "orders",
            () => null,
            () => true,
            () => false,
            () => 4,
            _ => { });

        sut.OpenSessionScope("session-a", knownTotalCount: 4);

        // Act
        await sut.LoadMessagesAsync();

        // Assert
        await operations.Received(1).PeekMessagesAsync(
            "orders",
            null,
            25,
            null,
            false,
            true,
            "session-a",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ClearSessionScope_RemovesActiveSessionSelection()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var preferences = Substitute.For<IPreferencesService>();
        preferences.MessagesPerPage.Returns(25);
        preferences.MaxTotalMessages.Returns(500);

        var logSink = Substitute.For<ILogSink>();
        var sut = new MessageOperationsViewModel(
            () => operations,
            preferences,
            logSink,
            () => "orders",
            () => null,
            () => true,
            () => false,
            () => 4,
            _ => { });

        sut.OpenSessionScope("session-a", knownTotalCount: 4);

        // Act
        sut.ClearSessionScope();

        // Assert
        sut.ScopedSessionId.Should().BeNull();
        sut.IsSessionScoped.Should().BeFalse();
    }

    [Fact]
    public async Task ClearSessionScope_ClearsVisibleStateAndResetsPagination()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.PeekMessagesAsync(
                "orders",
                null,
                Arg.Any<int>(),
                null,
                false,
                true,
                "session-a",
                Arg.Any<CancellationToken>())
            .Returns(
            [
                CreateMessage("msg-1", 1, DateTimeOffset.UtcNow.AddMinutes(-1)),
                CreateMessage("msg-2", 2, DateTimeOffset.UtcNow)
            ]);

        var preferences = Substitute.For<IPreferencesService>();
        preferences.MessagesPerPage.Returns(25);
        preferences.MaxTotalMessages.Returns(500);

        var logSink = Substitute.For<ILogSink>();
        var sut = new MessageOperationsViewModel(
            () => operations,
            preferences,
            logSink,
            () => "orders",
            () => null,
            () => true,
            () => false,
            () => 4,
            _ => { });

        sut.OpenSessionScope("session-a", knownTotalCount: 4);
        await sut.LoadMessagesAsync();

        sut.SelectMessage(sut.Messages[0]);
        sut.ToggleMultiSelectMode();
        sut.ToggleMessageSelection(sut.Messages[0]);

        // Act
        sut.ClearSessionScope();

        // Assert
        sut.ScopedSessionId.Should().BeNull();
        sut.IsSessionScoped.Should().BeFalse();
        sut.Messages.Should().BeEmpty();
        sut.FilteredMessages.Should().BeEmpty();
        sut.SelectedMessages.Should().BeEmpty();
        sut.SelectedMessage.Should().BeNull();
        sut.Pagination.CurrentPage.Should().Be(1);
        sut.Pagination.CanGoNext.Should().BeFalse();
        sut.Pagination.CanGoPrevious.Should().BeFalse();
        sut.Pagination.PageInfoText.Should().BeEmpty();
        sut.CanLoadNextPage.Should().BeFalse();
        sut.CanLoadPreviousPage.Should().BeFalse();
    }

    private static MessageInfo CreateMessage(string messageId, long sequenceNumber, DateTimeOffset enqueuedTime)
    {
        return new MessageInfo(
            messageId,
            null,
            null,
            $"body-{messageId}",
            enqueuedTime,
            null,
            sequenceNumber,
            0,
            "session-a",
            new Dictionary<string, object>());
    }
}
