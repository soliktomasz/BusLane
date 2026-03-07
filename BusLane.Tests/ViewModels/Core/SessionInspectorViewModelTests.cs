namespace BusLane.Tests.ViewModels.Core;

using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;

public class SessionInspectorViewModelTests
{
    [Fact]
    public async Task LoadSessionsAsync_PopulatesDiscoveredSessions()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var sessions = new[]
        {
            new SessionInspectorItem(
                "session-a",
                ActiveMessageCount: 4,
                DeadLetterMessageCount: 1,
                LastActivityAt: DateTimeOffset.UtcNow.AddMinutes(-3),
                LockedUntil: DateTimeOffset.UtcNow.AddMinutes(2),
                State: "ready"),
            new SessionInspectorItem(
                "session-b",
                ActiveMessageCount: 2,
                DeadLetterMessageCount: 0,
                LastActivityAt: DateTimeOffset.UtcNow.AddMinutes(-9),
                LockedUntil: null,
                State: null)
        };

        operations.GetSessionInspectorItemsAsync("orders", null, Arg.Any<CancellationToken>())
            .Returns(sessions);

        var preferences = CreatePreferences();
        var logSink = Substitute.For<ILogSink>();
        var messageOps = CreateMessageOperations(() => operations, preferences, logSink);
        var sut = new SessionInspectorViewModel(
            () => operations,
            messageOps,
            logSink,
            () => "orders",
            () => null,
            () => true,
            _ => { },
            _ => { });

        // Act
        await sut.LoadSessionsAsync();

        // Assert
        sut.Sessions.Should().HaveCount(2);
        sut.Sessions.Select(static s => s.SessionId).Should().ContainInOrder("session-a", "session-b");
    }

    [Fact]
    public async Task LoadSessionsAsync_WhenCalledAgainDuringAnInFlightLoad_AppliesLatestSessionsOnly()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var firstLoadStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLoadResult = new TaskCompletionSource<IReadOnlyList<SessionInspectorItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;

        var staleSession = new SessionInspectorItem(
            "session-stale",
            ActiveMessageCount: 1,
            DeadLetterMessageCount: 0,
            LastActivityAt: DateTimeOffset.UtcNow.AddMinutes(-12),
            LockedUntil: null,
            State: "stale");

        var freshSession = new SessionInspectorItem(
            "session-fresh",
            ActiveMessageCount: 3,
            DeadLetterMessageCount: 1,
            LastActivityAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            LockedUntil: DateTimeOffset.UtcNow.AddMinutes(4),
            State: "ready");

        operations.GetSessionInspectorItemsAsync("orders", null, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                invocationCount++;
                var ct = callInfo.ArgAt<CancellationToken>(2);

                if (invocationCount == 1)
                {
                    firstLoadStarted.TrySetResult(null);
                    return firstLoadResult.Task.WaitAsync(ct);
                }

                return Task.FromResult<IReadOnlyList<SessionInspectorItem>>([freshSession]);
            });

        var preferences = CreatePreferences();
        var logSink = Substitute.For<ILogSink>();
        var messageOps = CreateMessageOperations(() => operations, preferences, logSink);
        var sut = new SessionInspectorViewModel(
            () => operations,
            messageOps,
            logSink,
            () => "orders",
            () => null,
            () => true,
            _ => { },
            _ => { });

        sut.SelectedSession = staleSession;

        // Act
        var firstLoadTask = sut.LoadSessionsAsync();
        await firstLoadStarted.Task;

        var secondLoadTask = sut.LoadSessionsAsync();
        await secondLoadTask;

        firstLoadResult.TrySetResult([staleSession]);
        await firstLoadTask;

        // Assert
        await operations.Received(2).GetSessionInspectorItemsAsync("orders", null, Arg.Any<CancellationToken>());
        sut.Sessions.Should().ContainSingle().Which.SessionId.Should().Be("session-fresh");
        sut.SelectedSession.Should().BeNull();
        sut.IsLoadingSessions.Should().BeFalse();
    }

    [Fact]
    public async Task OpenSessionMessagesAsync_SetsMessageScopeAndSwitchesToActiveMessagesTab()
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

        var preferences = CreatePreferences();
        var logSink = Substitute.For<ILogSink>();
        var messageOps = CreateMessageOperations(() => operations, preferences, logSink);
        var selectedTabs = new List<int>();
        var sut = new SessionInspectorViewModel(
            () => operations,
            messageOps,
            logSink,
            () => "orders",
            () => null,
            () => true,
            selectedTabs.Add,
            _ => { });

        var session = new SessionInspectorItem(
            "session-a",
            ActiveMessageCount: 4,
            DeadLetterMessageCount: 1,
            LastActivityAt: DateTimeOffset.UtcNow.AddMinutes(-3),
            LockedUntil: DateTimeOffset.UtcNow.AddMinutes(2),
            State: "ready");

        // Act
        await sut.OpenSessionMessagesAsync(session);

        // Assert
        messageOps.ScopedSessionId.Should().Be("session-a");
        selectedTabs.Should().ContainSingle().Which.Should().Be(0);
        await operations.Received(1).PeekMessagesAsync(
            "orders",
            null,
            preferences.MessagesPerPage,
            null,
            false,
            true,
            "session-a",
            Arg.Any<CancellationToken>());
    }

    private static MessageOperationsViewModel CreateMessageOperations(
        Func<IServiceBusOperations?> getOperations,
        IPreferencesService preferences,
        ILogSink logSink)
    {
        return new MessageOperationsViewModel(
            getOperations,
            preferences,
            logSink,
            () => "orders",
            () => null,
            () => true,
            () => false,
            () => 4,
            _ => { });
    }

    private static IPreferencesService CreatePreferences()
    {
        var preferences = Substitute.For<IPreferencesService>();
        preferences.MessagesPerPage.Returns(25);
        preferences.MaxTotalMessages.Returns(500);
        preferences.DefaultMessageCount.Returns(25);
        return preferences;
    }
}
