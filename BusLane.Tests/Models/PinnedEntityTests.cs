namespace BusLane.Tests.Models;

using BusLane.Models;
using FluentAssertions;

public class PinnedEntityTests
{
    [Fact]
    public void Constructor_SubscriptionWithoutTopicName_Throws()
    {
        // Act
        var act = () => new PinnedEntity("workspace-a", PinnedEntityType.Subscription, "processor", null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TopicName*");
    }

    [Fact]
    public void Constructor_QueueWithTopicName_Throws()
    {
        // Act
        var act = () => new PinnedEntity("workspace-a", PinnedEntityType.Queue, "orders", "topic-a");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TopicName*");
    }

    [Fact]
    public void Constructor_TopicWithTopicName_Throws()
    {
        // Act
        var act = () => new PinnedEntity("workspace-a", PinnedEntityType.Topic, "orders-topic", "topic-a");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TopicName*");
    }
}
