namespace BusLane.Tests.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;

public class LineChartWidgetViewModelTests
{
    [Fact]
    public async Task MetricsBatchRecorded_RefreshesSeries()
    {
        // Arrange
        using var metricsService = new BatchOnlyMetricsService();
        var widget = new DashboardWidget
        {
            Type = WidgetType.LineChart,
            Configuration = new WidgetConfiguration
            {
                MetricName = "ActiveMessageCount",
                TimeRange = "1 Hour"
            }
        };
        var sut = new LineChartWidgetViewModel(widget, metricsService);

        // Act
        metricsService.RecordMetric("queue1", "ActiveMessageCount", 12);
        metricsService.EmitBatch();
        await Task.Delay(150);

        // Assert
        var series = sut.Series[0].Should().BeOfType<LineSeries<DateTimePoint>>().Subject;
        ((IEnumerable<DateTimePoint>)series.Values!).Should().ContainSingle();
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
