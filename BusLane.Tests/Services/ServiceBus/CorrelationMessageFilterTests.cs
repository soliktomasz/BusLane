namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;

public class CorrelationMessageFilterTests
{
    private readonly CorrelationMessageFilter _sut = new();

    [Fact]
    public void Matches_WithEmptyFilter_ReturnsTrue()
    {
        var message = CreateMessage();

        var result = _sut.Matches(message, CorrelationExplorerFilter.Empty);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("corr-orders")]
    [InlineData("message-1")]
    [InlineData("orders")]
    [InlineData("demo.servicebus")]
    [InlineData("created")]
    [InlineData("tenant")]
    [InlineData("north")]
    public void Matches_TextAcrossMessageFields_ReturnsTrue(string text)
    {
        var message = CreateMessage();
        var filter = CorrelationExplorerFilter.Empty with { Text = text };

        var result = _sut.Matches(message, filter);

        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_InclusiveTimeRange_IncludesBoundary()
    {
        var message = CreateMessage();
        var filter = CorrelationExplorerFilter.Empty with
        {
            From = message.EnqueuedTime,
            To = message.EnqueuedTime
        };

        var result = _sut.Matches(message, filter);

        result.Should().BeTrue();
    }

    [Fact]
    public void Matches_CombinedStructuredCriteria_UsesAndSemantics()
    {
        var message = CreateMessage();
        var matching = CorrelationExplorerFilter.Empty with
        {
            Namespace = "DEMO.SERVICEBUS.WINDOWS.NET",
            Entity = "ORDERS",
            Environment = ConnectionEnvironment.Test,
            Source = CorrelationMessageSource.Loaded,
            Identifier = "CORR-ORDERS"
        };
        var wrongSource = matching with { Source = CorrelationMessageSource.LiveStream };

        _sut.Matches(message, matching).Should().BeTrue();
        _sut.Matches(message, wrongSource).Should().BeFalse();
    }

    [Fact]
    public void Matches_PropertyKeyAndValue_RequiresMatchingProperty()
    {
        var message = CreateMessage();

        _sut.Matches(
                message,
                CorrelationExplorerFilter.Empty with
                {
                    PropertyKey = "TENANT",
                    PropertyValue = "NORTH"
                })
            .Should().BeTrue();
        _sut.Matches(
                message,
                CorrelationExplorerFilter.Empty with
                {
                    PropertyKey = "tenant",
                    PropertyValue = "south"
                })
            .Should().BeFalse();
    }

    [Fact]
    public void Matches_PropertyKeyWithoutValue_RequiresMatchingKey()
    {
        var message = CreateMessage();

        var result = _sut.Matches(
            message,
            CorrelationExplorerFilter.Empty with { PropertyKey = "missing" });

        result.Should().BeFalse();
    }

    private static CorrelationMessage CreateMessage() =>
        new(
            CorrelationMessageSource.Loaded,
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders",
            "Queue",
            null,
            null,
            "message-1",
            "corr-orders",
            "session-1",
            "application/json",
            """{"status":"created"}""",
            DateTimeOffset.Parse("2026-07-28T09:00:00Z"),
            1,
            new Dictionary<string, object> { ["tenant"] = "north" });
}
