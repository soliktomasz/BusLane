namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Input;
using BusLane.ViewModels.Dashboard;

public partial class NamespaceEntitySearchView : UserControl
{
    public NamespaceEntitySearchView()
    {
        InitializeComponent();
    }

    public void FocusSearch() => SearchBox.Focus();

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not NamespaceEntitySearchViewModel viewModel) return;

        switch (e.Key)
        {
            case Key.Down:
                viewModel.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                viewModel.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                viewModel.OpenSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                if (viewModel.Results.Count > 0 || viewModel.Query.Length > 0)
                {
                    viewModel.Clear();
                }
                else
                {
                    TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
                }
                e.Handled = true;
                break;
        }
    }
}
