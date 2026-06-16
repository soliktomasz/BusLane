namespace BusLane.ViewModels.Core;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Represents a single command palette action.
/// </summary>
public sealed record CommandPaletteItem(
    string Title,
    string Description,
    string Category,
    string Icon,
    Func<Task> ExecuteAsync)
{
    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Category.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Holds command palette state and search filtering.
/// </summary>
public partial class CommandPaletteViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredItems))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CommandPaletteItem? _selectedItem;

    public ObservableCollection<CommandPaletteItem> Items { get; } = [];

    public IReadOnlyList<CommandPaletteItem> FilteredItems =>
        Items.Where(item => item.Matches(SearchText)).ToList();

    public void Open(IEnumerable<CommandPaletteItem> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        SearchText = string.Empty;
        IsOpen = true;
        OnPropertyChanged(nameof(FilteredItems));
        SelectedItem = FilteredItems.FirstOrDefault();
    }

    public void Close()
    {
        IsOpen = false;
        SearchText = string.Empty;
        SelectedItem = null;
    }

    partial void OnSearchTextChanged(string value)
    {
        SelectedItem = FilteredItems.FirstOrDefault();
    }
}
