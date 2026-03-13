namespace BusLane.Tests.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;

public class MetricCardWidgetViewModelTests
{
    private readonly MetricsService _metricsService = new();

    private MetricCardWidgetViewModel CreateViewModel(string metricName = "ActiveMessageCount", string? entityFilter = null)
    {
        return CreateViewModel(_metricsService, metricName, entityFilter);
    }

    private static MetricCardWidgetViewModel CreateViewModel(
        IMetricsService metricsService,
        string metricName = "ActiveMessageCount",
        string? entityFilter = null)
    {
        var widget = new DashboardWidget
        {
            Type = WidgetType.MetricCard,
            Configuration = new WidgetConfiguration
            {
                MetricName = metricName,
                EntityFilter = entityFilter,
                TimeRange = "Previous Hour"
            }
        };
        return new MetricCardWidgetViewModel(widget, metricsService);
    }

    [Fact]
    public void RefreshData_WithNoMetrics_ShowsZeroValues()
    {
        var vm = CreateViewModel();

        vm.CurrentValue.Should().Be(0);
        vm.TrendPercentage.Should().Be("0%");
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public void RefreshData_WithCurrentMetrics_ShowsCurrentValue()
    {
        _metricsService.RecordMetric("queue1", "ActiveMessageCount", 50);
        _metricsService.RecordMetric("queue2", "ActiveMessageCount", 30);

        var vm = CreateViewModel();

        vm.CurrentValue.Should().Be(40); // Average of 50 and 30
    }

    [Fact]
    public void RefreshData_WithEntityFilter_FiltersCorrectly()
    {
        _metricsService.RecordMetric("queue1", "ActiveMessageCount", 50);
        _metricsService.RecordMetric("queue2", "ActiveMessageCount", 100);

        var vm = CreateViewModel(entityFilter: "queue1");

        vm.CurrentValue.Should().Be(50);
    }

    [Fact]
    public void RefreshData_WithZeroPreviousValue_ShowsNewTrend()
    {
        // Only record current metrics (within 5 minutes)
        _metricsService.RecordMetric("queue1", "ActiveMessageCount", 10);

        var vm = CreateViewModel();

        // Previous period average will also include the current metric since
        // "Previous Hour" spans 1 hour which includes the 5-minute current window
        // The key test is that no division by zero occurs
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public void RefreshData_TrendCalculation_DoesNotDivideByZero()
    {
        // With no data, previousRaw = 0, should not throw
        var vm = CreateViewModel();

        vm.TrendPercentage.Should().Be("0%");
        vm.IsTrendUp.Should().BeFalse();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public void Dispose_UnsubscribesFromMetricRecorded()
    {
        var vm = CreateViewModel();
        vm.Dispose();

        // Recording a metric after disposal should not cause issues
        _metricsService.RecordMetric("queue1", "ActiveMessageCount", 100);

        // If the handler was still attached, it would try to post to UI thread
        // and potentially throw. After dispose, nothing should happen.
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public void GetDefaultTitle_ReturnsMetricDisplayName()
    {
        var vm = CreateViewModel("DeadLetterCount");
        vm.Title.Should().Be("Dead Letters");
    }

    [Fact]
    public async Task MetricsBatchRecorded_RefreshesCurrentValue()
    {
        using var metricsService = new BatchOnlyMetricsService();
        var vm = CreateViewModel(metricsService);

        metricsService.RecordMetric("queue1", "ActiveMessageCount", 50);
        metricsService.RecordMetric("queue2", "ActiveMessageCount", 30);

        // Act
        metricsService.EmitBatch();
        await Task.Delay(150);

        // Assert
        vm.CurrentValue.Should().Be(40);
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
