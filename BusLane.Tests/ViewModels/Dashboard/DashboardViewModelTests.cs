namespace BusLane.Tests.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Services.Dashboard;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using NSubstitute;

public class DashboardViewModelTests
{
    private readonly IDashboardPersistenceService _persistenceService;
    private readonly DashboardLayoutEngine _layoutEngine;
    private readonly IMetricsService _metricsService;

    public DashboardViewModelTests()
    {
        _persistenceService = Substitute.For<IDashboardPersistenceService>();
        _layoutEngine = new DashboardLayoutEngine();
        _metricsService = Substitute.For<IMetricsService>();

        // Default: return empty dashboard
        _persistenceService.Load().Returns(new DashboardConfiguration { Widgets = [] });
    }

    private DashboardViewModel CreateViewModel()
    {
        return new DashboardViewModel(_persistenceService, _layoutEngine, _metricsService);
    }

    [Fact]
    public void Constructor_LoadsDashboardFromPersistence()
    {
        var config = new DashboardConfiguration
        {
            Widgets =
            [
                new DashboardWidget { Type = WidgetType.MetricCard, Configuration = new WidgetConfiguration() }
            ]
        };
        _persistenceService.Load().Returns(config);

        var vm = CreateViewModel();

        vm.Widgets.Should().HaveCount(1);
        vm.Widgets[0].Should().BeOfType<MetricCardWidgetViewModel>();
    }

    [Fact]
    public void AddWidget_AddsWidgetAndSaves()
    {
        var vm = CreateViewModel();

        vm.AddWidgetCommand.Execute(WidgetType.LineChart);

        vm.Widgets.Should().HaveCount(1);
        vm.Widgets[0].Should().BeOfType<LineChartWidgetViewModel>();
        _persistenceService.Received(1).Save(Arg.Is<DashboardConfiguration>(c => c.Widgets.Count == 1));
    }

    [Fact]
    public void RemoveWidget_RemovesWidgetDisposesAndSaves()
    {
        var config = new DashboardConfiguration
        {
            Widgets =
            [
                new DashboardWidget { Type = WidgetType.MetricCard, Configuration = new WidgetConfiguration() }
            ]
        };
        _persistenceService.Load().Returns(config);

        var vm = CreateViewModel();
        var widget = vm.Widgets[0];

        vm.RemoveWidgetCommand.Execute(widget);

        vm.Widgets.Should().BeEmpty();
        _persistenceService.Received(1).Save(Arg.Is<DashboardConfiguration>(c => c.Widgets.Count == 0));
    }

    [Fact]
    public void AddWidget_WhenAtMaxWidgets_DoesNotAdd()
    {
        var widgets = Enumerable.Range(0, 20).Select(i => new DashboardWidget
        {
            Type = WidgetType.MetricCard,
            Row = i / 2,
            Column = (i % 2) * 6,
            Width = 6,
            Height = 4,
            Configuration = new WidgetConfiguration()
        }).ToList();

        _persistenceService.Load().Returns(new DashboardConfiguration { Widgets = widgets });

        var vm = CreateViewModel();
        vm.Widgets.Should().HaveCount(20);
        vm.CanAddWidget.Should().BeFalse();

        vm.AddWidgetCommand.Execute(WidgetType.LineChart);

        vm.Widgets.Should().HaveCount(20);
    }

    [Fact]
    public void AddWidget_CreatesCorrectWidgetType()
    {
        var vm = CreateViewModel();

        vm.AddWidgetCommand.Execute(WidgetType.PieChart);
        vm.Widgets[0].Should().BeOfType<PieChartWidgetViewModel>();

        vm.AddWidgetCommand.Execute(WidgetType.BarChart);
        vm.Widgets[1].Should().BeOfType<BarChartWidgetViewModel>();
    }

    [Fact]
    public void CanAddWidget_UpdatesAfterAddAndRemove()
    {
        var vm = CreateViewModel();

        vm.CanAddWidget.Should().BeTrue();

        vm.AddWidgetCommand.Execute(WidgetType.MetricCard);
        vm.CanAddWidget.Should().BeTrue();

        vm.RemoveWidgetCommand.Execute(vm.Widgets[0]);
        vm.CanAddWidget.Should().BeTrue();
    }
}
