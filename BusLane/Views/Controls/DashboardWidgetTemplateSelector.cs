namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Metadata;
using BusLane.ViewModels.Dashboard;

public class DashboardWidgetTemplateSelector : IDataTemplate
{
    [Content]
    public required DataTemplate LineChartTemplate { get; set; }
    public required DataTemplate PieChartTemplate { get; set; }
    public required DataTemplate BarChartTemplate { get; set; }
    public required DataTemplate MetricCardTemplate { get; set; }

    public Control? Build(object? param)
    {
        if (param is LineChartWidgetViewModel)
            return LineChartTemplate.Build(param);
        if (param is PieChartWidgetViewModel)
            return PieChartTemplate.Build(param);
        if (param is BarChartWidgetViewModel)
            return BarChartTemplate.Build(param);
        if (param is MetricCardWidgetViewModel)
            return MetricCardTemplate.Build(param);

        return null;
    }

    public bool Match(object? data)
    {
        return data is DashboardWidgetViewModel;
    }
}
