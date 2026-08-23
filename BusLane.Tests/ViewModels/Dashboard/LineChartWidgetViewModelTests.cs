namespace BusLane.Tests.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;

public class LineChartWidgetViewModelTests
{
    [Fact]
    public async Task MetricsBatchRecorded_RefreshesPlotData()
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
        using var sut = new LineChartWidgetViewModel(widget, metricsService);
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LineChartWidgetViewModel.PlotData) && sut.PlotData?.Points.Count > 0)
            {
                refreshed.TrySetResult();
            }
        };

        // Act
        metricsService.RecordMetric("queue1", "ActiveMessageCount", 12);
        metricsService.EmitBatch();
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        sut.PlotData.Should().NotBeNull();
        sut.PlotData!.Points.Should().NotBeEmpty();
        sut.PlotData!.LineColorToken.Should().Be("AccentBrand");
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
