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
    public void Query_SubstringOrSubsequence_MatchesExpectedEntity(string query, string expected)
    {
        var _sut = new NamespaceEntitySearchViewModel(_ => { });
        _sut.UpdateInventory([Queue("orders"), Queue("orders-eu")], [], []);

        _sut.Query = query;

        _sut.Results.Should().Contain(item => item.EntityName == expected);
    }

    [Fact]
    public void OpenSelected_SubscriptionUsesFullPathAndActiveDestination()
    {
        NamespaceNavigationRequest? opened = null;
        var _sut = new NamespaceEntitySearchViewModel(request => opened = request);
        _sut.UpdateInventory([], [], [Subscription("payments", "fraud-indexer")]);
        _sut.Query = "fraud";
        _sut.SelectedResult = _sut.Results.Single();

        _sut.OpenSelectedCommand.Execute(null);

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
        var _sut = new NamespaceEntitySearchViewModel(request => opened = request);
        var topics = Enumerable.Range(0, 40)
            .Select(index => new TopicInfo($"topic-{index:00}", 0, 0, null, TimeSpan.FromDays(14)))
            .ToList();
        _sut.UpdateInventory([], topics, []);

        _sut.Query = "topic";
        _sut.SelectedResult = _sut.Results[0];
        _sut.OpenSelectedCommand.Execute(null);

        _sut.Results.Should().HaveCount(30);
        opened!.View.Should().Be(EntityWorkspaceView.TopicSubscriptions);
        opened.EntityType.Should().Be(EntityType.Topic);
    }

    private static QueueInfo Queue(string name) =>
        new(name, 0, 0, 0, 0, 0, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

    private static SubscriptionInfo Subscription(string topic, string name) =>
        new(name, topic, 0, 0, 0, null, false);
}
