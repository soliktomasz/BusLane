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
        sut.Pagination.HasPageInfo.Should().BeFalse();
        sut.Pagination.PageLabel.Should().BeEmpty();
        sut.Pagination.PageRangeText.Should().BeEmpty();
        sut.Pagination.PageDetailText.Should().BeNull();
        sut.CanLoadNextPage.Should().BeFalse();
        sut.CanLoadPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task LoadMessagesAsync_WhenKnownTotalExists_SetsPagerDetailText()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.PeekMessagesAsync(
                "orders",
                null,
                Arg.Any<int>(),
                null,
                false,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(CreateMessages(1, 25));

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
            () => false,
            () => false,
            () => 60,
            _ => { });

        // Act
        await sut.LoadMessagesAsync();

        // Assert
        sut.Pagination.HasPageInfo.Should().BeTrue();
        sut.Pagination.PageLabel.Should().Be("Page 1");
        sut.Pagination.PageRangeText.Should().Be("Showing 1-25");
        sut.Pagination.PageDetailText.Should().Be("of 60 messages");
    }

    [Fact]
    public async Task LoadPreviousPage_WhenTotalUnknown_DoesNotUseCachedCountAsKnownTotal()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.PeekMessagesAsync(
                "orders",
                null,
                Arg.Any<int>(),
                Arg.Any<long?>(),
                false,
                false,
                null,
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var fromSequenceNumber = callInfo.ArgAt<long?>(3);
                return fromSequenceNumber switch
                {
                    null => CreateMessages(1, 25),
                    26 => CreateMessages(26, 25),
                    _ => Array.Empty<MessageInfo>()
                };
            });

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
            () => false,
            () => false,
            () => 0,
            _ => { });

        await sut.LoadMessagesAsync();
        await sut.LoadNextPageCommand.ExecuteAsync(null);

        // Act
        sut.LoadPreviousPageCommand.Execute(null);

        // Assert
        sut.Pagination.PageLabel.Should().Be("Page 1");
        sut.Pagination.PageRangeText.Should().Be("Showing 1-25");
        sut.Pagination.PageDetailText.Should().BeNull();
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

    private static IEnumerable<MessageInfo> CreateMessages(long startingSequenceNumber, int count)
    {
        return Enumerable.Range(0, count)
            .Select(offset => CreateMessage(
                $"msg-{startingSequenceNumber + offset}",
                startingSequenceNumber + offset,
                DateTimeOffset.UtcNow.AddMinutes(-(startingSequenceNumber + offset))))
            .ToArray();
    }
}
