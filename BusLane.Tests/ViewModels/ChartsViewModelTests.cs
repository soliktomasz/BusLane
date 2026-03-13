namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Services.Monitoring;
using BusLane.ViewModels;
using FluentAssertions;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;

public class ChartsViewModelTests
{
    [Fact]
    public async Task MetricsBatchRecorded_RefreshesCharts()
    {
        // Arrange
        using var metricsService = new BatchOnlyMetricsService();
        var sut = new ChartsViewModel(metricsService);

        // Act
        metricsService.RecordMetric("queue1", "ActiveMessageCount", 10);
        metricsService.RecordMetric("queue1", "DeadLetterCount", 4);
        metricsService.EmitBatch();
        await Task.Delay(50);

        // Assert
        var activeSeries = sut.MessageCountSeries[0].Should().BeOfType<LineSeries<DateTimePoint>>().Subject;
        var deadLetterSeries = sut.DeadLetterSeries[0].Should().BeOfType<LineSeries<DateTimePoint>>().Subject;
        ((IEnumerable<DateTimePoint>)activeSeries.Values!).Should().NotBeEmpty();
        ((IEnumerable<DateTimePoint>)deadLetterSeries.Values!).Should().NotBeEmpty();
    }

    private sealed class BatchOnlyMetricsService : IMetricsService
    {
        private readonly List<MetricDataPoint> _metrics = [];

        public event EventHandler<MetricDataPoint>? MetricRecorded
        {
            add { }
            remove { }
        }
        public event EventHandler<IReadOnlyList<MetricDataPoint>>? MetricsBatchRecorded;

        public void RecordMetric(string entityName, string metricName, double value)
        {
            _metrics.Add(new MetricDataPoint(DateTimeOffset.UtcNow, entityName, metricName, value));
        }

        public IEnumerable<MetricDataPoint> GetMetricHistory(string entityName, string metricName, TimeSpan duration)
        {
            var cutoff = DateTimeOffset.UtcNow - duration;
            return _metrics.Where(metric =>
                metric.EntityName == entityName &&
                metric.MetricName == metricName &&
                metric.Timestamp >= cutoff);
        }

        public IEnumerable<MetricDataPoint> GetEntityMetrics(string entityName, TimeSpan duration)
        {
            var cutoff = DateTimeOffset.UtcNow - duration;
            return _metrics.Where(metric => metric.EntityName == entityName && metric.Timestamp >= cutoff);
        }

        public IEnumerable<MetricDataPoint> GetAggregatedMetrics(string metricName, TimeSpan duration)
        {
            var cutoff = DateTimeOffset.UtcNow - duration;
            return _metrics.Where(metric => metric.MetricName == metricName && metric.Timestamp >= cutoff);
        }

        public void CleanupOldMetrics(TimeSpan retentionPeriod)
        {
            var cutoff = DateTimeOffset.UtcNow - retentionPeriod;
            _metrics.RemoveAll(metric => metric.Timestamp < cutoff);
        }

        public void EmitBatch()
        {
            MetricsBatchRecorded?.Invoke(this, _metrics.ToList().AsReadOnly());
        }

        public void Dispose()
        {
        }
    }
}
