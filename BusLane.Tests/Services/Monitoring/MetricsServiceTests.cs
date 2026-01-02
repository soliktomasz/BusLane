using BusLane.Models;
using BusLane.Services.Monitoring;
using FluentAssertions;

namespace BusLane.Tests.Services.Monitoring;

public class MetricsServiceTests
{
    private readonly MetricsService _sut;

    public MetricsServiceTests()
    {
        _sut = new MetricsService();
    }

    [Fact]
    public void RecordMetric_WithValidData_StoresMetricPoint()
    {
        // Arrange
        const string entityName = "test-queue";
        const string metricName = "ActiveMessageCount";
        const double value = 42.0;

        // Act
        _sut.RecordMetric(entityName, metricName, value);
        var history = _sut.GetMetricHistory(entityName, metricName, TimeSpan.FromMinutes(1));

        // Assert
        history.Should().ContainSingle()
            .Which.Should().Match<MetricDataPoint>(p => 
                p.EntityName == entityName && 
                p.MetricName == metricName && 
                p.Value == value);
    }

    [Fact]
    public void RecordMetric_RaisesMetricRecordedEvent()
    {
        // Arrange
        MetricDataPoint? capturedDataPoint = null;
        _sut.MetricRecorded += (_, dp) => capturedDataPoint = dp;

        // Act
        _sut.RecordMetric("queue1", "MessageCount", 100);

        // Assert
        capturedDataPoint.Should().NotBeNull();
        capturedDataPoint!.EntityName.Should().Be("queue1");
        capturedDataPoint.MetricName.Should().Be("MessageCount");
        capturedDataPoint.Value.Should().Be(100);
    }

    [Fact]
    public void GetMetricHistory_WithMultiplePoints_ReturnsOnlyPointsWithinDuration()
    {
        // Arrange
        _sut.RecordMetric("queue", "metric", 1);
        _sut.RecordMetric("queue", "metric", 2);
        _sut.RecordMetric("queue", "metric", 3);

        // Act - Get points from the last minute (all should be included)
        var history = _sut.GetMetricHistory("queue", "metric", TimeSpan.FromMinutes(1));

        // Assert
        history.Should().HaveCount(3);
    }

    [Fact]
    public void GetMetricHistory_WithNonExistentMetric_ReturnsEmptyCollection()
    {
        // Act
        var history = _sut.GetMetricHistory("non-existent", "metric", TimeSpan.FromMinutes(1));

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public void GetEntityMetrics_ReturnsAllMetricsForEntity()
    {
        // Arrange
        _sut.RecordMetric("queue1", "ActiveMessageCount", 10);
        _sut.RecordMetric("queue1", "DeadLetterCount", 5);
        _sut.RecordMetric("queue2", "ActiveMessageCount", 20);

        // Act
        var metrics = _sut.GetEntityMetrics("queue1", TimeSpan.FromMinutes(1));

        // Assert
        metrics.Should().HaveCount(2);
        metrics.Should().Contain(m => m.MetricName == "ActiveMessageCount" && m.Value == 10);
        metrics.Should().Contain(m => m.MetricName == "DeadLetterCount" && m.Value == 5);
    }

    [Fact]
    public void GetEntityMetrics_ExcludesOtherEntities()
    {
        // Arrange
        _sut.RecordMetric("queue1", "ActiveMessageCount", 10);
        _sut.RecordMetric("queue2", "ActiveMessageCount", 20);

        // Act
        var metrics = _sut.GetEntityMetrics("queue1", TimeSpan.FromMinutes(1));

        // Assert
        metrics.Should().ContainSingle();
        metrics.First().Value.Should().Be(10);
    }

    [Fact]
    public void GetAggregatedMetrics_ReturnsMetricAcrossAllEntities()
    {
        // Arrange
        _sut.RecordMetric("queue1", "ActiveMessageCount", 10);
        _sut.RecordMetric("queue2", "ActiveMessageCount", 20);
        _sut.RecordMetric("queue3", "ActiveMessageCount", 30);

        // Act
        var metrics = _sut.GetAggregatedMetrics("ActiveMessageCount", TimeSpan.FromMinutes(1));

        // Assert
        metrics.Should().HaveCount(3);
        metrics.Sum(m => m.Value).Should().Be(60);
    }

    [Fact]
    public void GetAggregatedMetrics_ExcludesOtherMetricNames()
    {
        // Arrange
        _sut.RecordMetric("queue1", "ActiveMessageCount", 10);
        _sut.RecordMetric("queue1", "DeadLetterCount", 5);

        // Act
        var metrics = _sut.GetAggregatedMetrics("ActiveMessageCount", TimeSpan.FromMinutes(1));

        // Assert
        metrics.Should().ContainSingle();
        metrics.First().MetricName.Should().Be("ActiveMessageCount");
    }

    [Fact]
    public void CleanupOldMetrics_RemovesExpiredPoints()
    {
        // Arrange - Record a metric
        _sut.RecordMetric("queue", "metric", 100);
        
        // Act - Cleanup with zero retention (removes all)
        _sut.CleanupOldMetrics(TimeSpan.Zero);
        var history = _sut.GetMetricHistory("queue", "metric", TimeSpan.FromHours(1));

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public void CleanupOldMetrics_PreservesRecentPoints()
    {
        // Arrange
        _sut.RecordMetric("queue", "metric", 100);

        // Act - Cleanup with 1 hour retention (point should remain)
        _sut.CleanupOldMetrics(TimeSpan.FromHours(1));
        var history = _sut.GetMetricHistory("queue", "metric", TimeSpan.FromHours(1));

        // Assert
        history.Should().ContainSingle();
    }

    [Fact]
    public void RecordMetric_WithManyPoints_EnforcesMaxLimit()
    {
        // Arrange - Record more than the max limit (1000)
        for (int i = 0; i < 1100; i++)
        {
            _sut.RecordMetric("queue", "metric", i);
        }

        // Act
        var history = _sut.GetMetricHistory("queue", "metric", TimeSpan.FromHours(24)).ToList();

        // Assert - Should be capped at 1000
        history.Should().HaveCount(1000);
        // First points should be removed, so values should start from 100 (1100 - 1000)
        history.First().Value.Should().Be(100);
        history.Last().Value.Should().Be(1099);
    }

    [Fact]
    public void GetEntityMetrics_ReturnsOrderedByTimestamp()
    {
        // Arrange
        _sut.RecordMetric("queue", "Metric1", 1);
        _sut.RecordMetric("queue", "Metric2", 2);
        _sut.RecordMetric("queue", "Metric3", 3);

        // Act
        var metrics = _sut.GetEntityMetrics("queue", TimeSpan.FromMinutes(1)).ToList();

        // Assert
        metrics.Should().BeInAscendingOrder(m => m.Timestamp);
    }

    [Fact]
    public void GetAggregatedMetrics_ReturnsOrderedByTimestamp()
    {
        // Arrange
        _sut.RecordMetric("queue1", "metric", 1);
        _sut.RecordMetric("queue2", "metric", 2);
        _sut.RecordMetric("queue3", "metric", 3);

        // Act
        var metrics = _sut.GetAggregatedMetrics("metric", TimeSpan.FromMinutes(1)).ToList();

        // Assert
        metrics.Should().BeInAscendingOrder(m => m.Timestamp);
    }
}

