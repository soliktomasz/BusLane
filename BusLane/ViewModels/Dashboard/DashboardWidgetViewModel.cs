namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public abstract partial class DashboardWidgetViewModel : ViewModelBase
{
    [ObservableProperty] private DashboardWidget _widget;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public Guid Id => Widget.Id;
    public WidgetType Type => Widget.Type;

    public int Row
    {
        get => Widget.Row;
        set
        {
            if (Widget.Row != value)
            {
                Widget.Row = value;
                OnPropertyChanged();
            }
        }
    }

    public int Column
    {
        get => Widget.Column;
        set
        {
            if (Widget.Column != value)
            {
                Widget.Column = value;
                OnPropertyChanged();
            }
        }
    }

    public int Width
    {
        get => Widget.Width;
        set
        {
            if (Widget.Width != value)
            {
                Widget.Width = value;
                OnPropertyChanged();
            }
        }
    }

    public int Height
    {
        get => Widget.Height;
        set
        {
            if (Widget.Height != value)
            {
                Widget.Height = value;
                OnPropertyChanged();
            }
        }
    }

    protected DashboardWidgetViewModel(DashboardWidget widget)
    {
        _widget = widget;
        Title = widget.Configuration.Title ?? GetDefaultTitle();
    }

    protected abstract string GetDefaultTitle();
    public abstract void RefreshData();

    protected void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    protected void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void Retry()
    {
        ClearError();
        RefreshData();
    }
}
