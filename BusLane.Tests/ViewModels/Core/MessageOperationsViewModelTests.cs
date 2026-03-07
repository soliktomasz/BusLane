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
}
