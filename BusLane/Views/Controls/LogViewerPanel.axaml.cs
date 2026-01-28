using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BusLane.Views.Controls;

/// <summary>
/// Panel displaying activity logs with filtering and search capabilities.
/// </summary>
public partial class LogViewerPanel : UserControl
{
    public static readonly StyledProperty<ICommand> CloseCommandProperty =
        AvaloniaProperty.Register<LogViewerPanel, ICommand>(nameof(CloseCommand));

    public ICommand CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public LogViewerPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
