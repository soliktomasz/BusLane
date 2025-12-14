using Avalonia.Controls;
using BusLane.ViewModels;

namespace BusLane.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Try to restore previous session when window loads
        Loaded += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }
}
