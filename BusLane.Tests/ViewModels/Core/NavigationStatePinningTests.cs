namespace BusLane.Tests.ViewModels.Core;

using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.ViewModels.Core;
using FluentAssertions;

public class NavigationStatePinningTests
{
    [Fact]
    public void TogglePin_Queue_AddsScopedPinAndPersistsJson()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var sut = new NavigationState(preferences);
        sut.SetPinScope("workspace-a");
        var queue = CreateQueue("orders");

        // Act
        sut.TogglePin(queue);

        // Assert
        sut.IsPinned(queue).Should().BeTrue();
        sut.PinnedEntities.Should().ContainSingle(pin =>
            pin.WorkspaceId == "workspace-a" &&
            pin.Type == PinnedEntityType.Queue &&
            pin.Name == "orders");
        preferences.PinnedEntitiesJson.Should().Contain("orders");
        preferences.SaveCount.Should().Be(1);
    }

    [Fact]
    public void TogglePin_PinnedQueue_RemovesScopedPinAndPersistsJson()
    {
        // Arrange
        var preferences = new TestPreferencesService
        {
            PinnedEntitiesJson = """
                [{"WorkspaceId":"workspace-a","Type":"Queue","Name":"orders","TopicName":null}]
                """
        };
        var sut = new NavigationState(preferences);
        sut.SetPinScope("workspace-a");
        var queue = CreateQueue("orders");

        // Act
        sut.TogglePin(queue);

        // Assert
        sut.IsPinned(queue).Should().BeFalse();
        sut.PinnedEntities.Should().BeEmpty();
        preferences.PinnedEntitiesJson.Should().Be("[]");
        preferences.SaveCount.Should().Be(1);
    }

    [Fact]
    public void FilteredQueues_PutsPinnedQueuesFirst()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var sut = new NavigationState(preferences);
        sut.SetPinScope("workspace-a");
        var alpha = CreateQueue("alpha");
        var beta = CreateQueue("beta");
        sut.Queues.Add(alpha);
        sut.Queues.Add(beta);
        sut.TogglePin(beta);

        // Act
        var result = sut.FilteredQueues.Select(queue => queue.Name);

        // Assert
        result.Should().Equal("beta", "alpha");
    }

    [Fact]
    public void SetPinScope_LoadsOnlyPinsForCurrentWorkspace()
    {
        // Arrange
        var preferences = new TestPreferencesService
        {
            PinnedEntitiesJson = """
                [
                  {"WorkspaceId":"workspace-a","Type":"Queue","Name":"orders","TopicName":null},
                  {"WorkspaceId":"workspace-b","Type":"Queue","Name":"billing","TopicName":null}
                ]
                """
        };
        var sut = new NavigationState(preferences);

        // Act
        sut.SetPinScope("workspace-b");

        // Assert
        sut.PinnedEntities.Should().ContainSingle(pin => pin.Name == "billing");
    }

    [Fact]
    public void TogglePin_Subscription_UsesTopicNameInIdentity()
    {
        // Arrange
        var preferences = new TestPreferencesService();
        var sut = new NavigationState(preferences);
        sut.SetPinScope("workspace-a");
        var subscription = new SubscriptionInfo("processor", "orders-topic", 1, 1, 0, null, false);

        // Act
        sut.TogglePin(subscription);

        // Assert
        sut.IsPinned(subscription).Should().BeTrue();
        sut.PinnedEntities.Should().ContainSingle(pin =>
            pin.Type == PinnedEntityType.Subscription &&
            pin.TopicName == "orders-topic" &&
            pin.Name == "processor");
    }

    private static QueueInfo CreateQueue(string name) =>
        new(name, 1, 1, 0, 0, 1, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

    private sealed class TestPreferencesService : IPreferencesService
    {
        public bool ConfirmBeforeDelete { get; set; } = true;
        public bool ConfirmBeforePurge { get; set; } = true;
        public bool AutoRefreshMessages { get; set; }
        public int AutoRefreshIntervalSeconds { get; set; } = 30;
        public int DefaultMessageCount { get; set; } = 100;
        public int MessagesPerPage { get; set; } = 100;
        public int MaxTotalMessages { get; set; } = 500;
        public bool ShowDeadLetterBadges { get; set; } = true;
        public bool EnableMessagePreview { get; set; } = true;
        public bool ShowNavigationPanel { get; set; } = true;
        public bool ShowTerminalPanel { get; set; }
        public bool TerminalIsDocked { get; set; } = true;
        public double TerminalDockHeight { get; set; } = 260;
        public string? TerminalWindowBoundsJson { get; set; }
        public string Theme { get; set; } = "System";
        public int LiveStreamPollingIntervalSeconds { get; set; } = 1;
        public bool RestoreTabsOnStartup { get; set; } = true;
        public string OpenTabsJson { get; set; } = "[]";
        public string PinnedEntitiesJson { get; set; } = "[]";
        public bool EnableTelemetry { get; set; }
        public bool AutoCheckForUpdates { get; set; } = true;
        public string? SkippedUpdateVersion { get; set; }
        public DateTime? UpdateRemindLaterDate { get; set; }
        public int SaveCount { get; private set; }

        public event EventHandler? PreferencesChanged
        {
            add { }
            remove { }
        }

        public void Save() => SaveCount++;

        public void Load()
        {
        }
    }
}
