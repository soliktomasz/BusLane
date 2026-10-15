namespace BusLane.Tests.ViewModels.Core;

using BusLane.Models;
using BusLane.ViewModels.Core;
using FluentAssertions;

public class NavigationStateEntityBrowserTests
{
    private readonly NavigationState _sut = new();

    [Fact]
    public void EntityFilter_SubscriptionMatches_KeepsAndExpandsParent()
    {
        // Arrange
        var topic = CreateTopic();
        _sut.Topics.Add(topic);

        // Act
        _sut.EntityFilter = " BILLING ";

        // Assert
        _sut.FilteredTopics.Should().ContainSingle().Which.Should().BeSameAs(topic);
        topic.IsExpanded.Should().BeTrue();
        topic.FilteredSubscriptions.Should().ContainSingle().Which.Name.Should().Be("billing");
    }

    [Fact]
    public void EntityFilter_Cleared_RestoresChildrenAndExpansion()
    {
        // Arrange
        var topic = CreateTopic();
        _sut.Topics.Add(topic);
        _sut.EntityFilter = "billing";

        // Act
        _sut.EntityFilter = string.Empty;

        // Assert
        topic.FilteredSubscriptions.Should().HaveCount(2);
        topic.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void EntityFilter_TopicMatches_ShowsAllChildren()
    {
        // Arrange
        var topic = CreateTopic();
        _sut.Topics.Add(topic);

        // Act
        _sut.EntityFilter = "orders";

        // Assert
        _sut.FilteredTopics.Should().ContainSingle();
        topic.FilteredSubscriptions.Should().HaveCount(2);
    }

    [Fact]
    public void Subscriptions_LoadedDuringSearch_RefreshesResults()
    {
        // Arrange
        var topic = new TopicInfo { Name = "orders" };
        _sut.Topics.Add(topic);
        _sut.EntityFilter = "billing";
        var notifications = new List<string?>();
        _sut.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        // Act
        topic.Subscriptions.Add(new SubscriptionInfo("billing", "orders", 1, 0, 0, null, false));

        // Assert
        _sut.FilteredTopics.Should().ContainSingle();
        notifications.Should().Contain(nameof(NavigationState.FilteredTopics));
        _sut.HasNoMatchingEntities.Should().BeFalse();
    }

    [Fact]
    public void Topics_Cleared_DetachesRemovedTopics()
    {
        // Arrange
        var topic = CreateTopic();
        _sut.Topics.Add(topic);
        _sut.Topics.Clear();
        var notifications = new List<string?>();
        _sut.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        // Act
        topic.Subscriptions.Clear();

        // Assert
        notifications.Should().NotContain(nameof(NavigationState.FilteredTopics));
    }

    [Fact]
    public void SelectedEntity_Subscription_ExposesFullPathAndKind()
    {
        // Act
        _sut.SelectedEntity = new SubscriptionInfo("billing", "orders", 1, 0, 0, null, false);

        // Assert
        _sut.SelectedEntityPath.Should().Be("orders / billing");
        _sut.SelectedEntityKind.Should().Be("Subscription");
        _sut.HasSelectedEntity.Should().BeTrue();
    }

    private static TopicInfo CreateTopic()
    {
        var topic = new TopicInfo { Name = "orders", SubscriptionsLoaded = true };
        topic.Subscriptions.Add(new SubscriptionInfo("billing", "orders", 1, 0, 0, null, false));
        topic.Subscriptions.Add(new SubscriptionInfo("analytics", "orders", 0, 0, 0, null, false));
        return topic;
    }
}
