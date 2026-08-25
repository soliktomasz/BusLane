namespace BusLane.Tests.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;

public class NamespaceEntitySearchViewModelTests
{
    [Theory]
    [InlineData("ord", "orders")]
    [InlineData("oreu", "orders-eu")]
    public void Query_MatchesSubstringAndSubsequence(string query, string expected)
    {
        var sut = new NamespaceEntitySearchViewModel(_ => { });
        sut.UpdateInventory([Queue("orders"), Queue("orders-eu")], [], []);

        sut.Query = query;

        sut.Results.Should().Contain(item => item.EntityName == expected);
    }

    [Fact]
    public void OpenSelected_SubscriptionUsesFullPathAndActiveDestination()
    {
        NamespaceNavigationRequest? opened = null;
        var sut = new NamespaceEntitySearchViewModel(request => opened = request);
        sut.UpdateInventory([], [], [Subscription("payments", "fraud-indexer")]);
        sut.Query = "fraud";
        sut.SelectedResult = sut.Results.Single();

        sut.OpenSelectedCommand.Execute(null);

        opened.Should().Be(new NamespaceNavigationRequest(
            EntityType.Subscription,
            "payments/fraud-indexer",
            "payments",
            EntityWorkspaceView.ActiveMessages));
    }

    [Fact]
    public void Query_CapsResultsAtThirtyAndTopicsOpenSubscriptions()
    {
        NamespaceNavigationRequest? opened = null;
        var sut = new NamespaceEntitySearchViewModel(request => opened = request);
        var topics = Enumerable.Range(0, 40)
            .Select(index => new TopicInfo($"topic-{index:00}", 0, 0, null, TimeSpan.FromDays(14)))
            .ToList();
        sut.UpdateInventory([], topics, []);

        sut.Query = "topic";
        sut.SelectedResult = sut.Results[0];
        sut.OpenSelectedCommand.Execute(null);

        sut.Results.Should().HaveCount(30);
        opened!.View.Should().Be(EntityWorkspaceView.TopicSubscriptions);
        opened.EntityType.Should().Be(EntityType.Topic);
    }

    private static QueueInfo Queue(string name) =>
        new(name, 0, 0, 0, 0, 0, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

    private static SubscriptionInfo Subscription(string topic, string name) =>
        new(name, topic, 0, 0, 0, null, false);
}
