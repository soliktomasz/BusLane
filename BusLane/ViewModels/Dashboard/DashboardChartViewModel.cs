using BusLane.Models.Dashboard;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia;
using Avalonia.Threading;
using System.Collections.Generic;
using System;
using System.Linq;

namespace BusLane.ViewModels.Dashboard;

public partial class DashboardChartViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private LinePlotData? _plotData;

    [ObservableProperty]
    private string _selectedTimeRange = "1 Hour";

    [ObservableProperty]
    private bool _useGlobalTimeRange = true;

    public event EventHandler<string>? TimeRangeChanged;

    public string[] TimeRangeOptions { get; } = new[]
    {
        "15 Minutes",
        "1 Hour",
        "6 Hours",
        "24 Hours"
    };

    public DashboardChartViewModel(string title)
    {
        _title = title;
    }

    partial void OnSelectedTimeRangeChanged(string value)
    {
        TimeRangeChanged?.Invoke(this, value);
    }

    public void SetGlobalTimeRange(string timeRange)
    {
        if (UseGlobalTimeRange)
        {
            SelectedTimeRange = timeRange;
        }
    }

    public void UpdateData(IEnumerable<LinePlotPoint> dataPoints)
    {
        if (Application.Current is not null && !Dispatcher.UIThread.CheckAccess())
        {
            var snapshot = dataPoints.ToList();
            Dispatcher.UIThread.Post(() => UpdateData(snapshot));
            return;
        }

        var points = dataPoints.ToList();
        PlotData = new LinePlotData(Title, points, GetColorToken());
    }

    private string GetColorToken()
    {
        if (Title.Contains("Dead", StringComparison.OrdinalIgnoreCase))
        {
            return "TextDanger";
        }

        if (Title.Contains("Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return "TextWarning";
        }

        if (Title.Contains("Size", StringComparison.OrdinalIgnoreCase))
        {
            return "TextSuccess";
        }

        return "AccentBrand";
    }
}
