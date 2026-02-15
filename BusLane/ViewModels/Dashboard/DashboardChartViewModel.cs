using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using System.Collections.ObjectModel;

namespace BusLane.ViewModels.Dashboard;

public partial class DashboardChartViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private ObservableCollection<ISeries> _series = new();

    [ObservableProperty]
    private string _selectedTimeRange = "1 Hour";

    [ObservableProperty]
    private bool _useGlobalTimeRange = true;

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

    public void SetGlobalTimeRange(string timeRange)
    {
        if (UseGlobalTimeRange)
        {
            SelectedTimeRange = timeRange;
        }
    }

    public void UpdateData(double[] dataPoints)
    {
        // TODO: Update chart series with new data
        // This will be implemented when we wire up the charts
    }
}
