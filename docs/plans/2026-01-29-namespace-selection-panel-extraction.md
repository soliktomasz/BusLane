# Namespace Selection Panel Extraction

**Goal:** Improve MainWindow.axaml readability and maintainability by extracting the namespace selection panel (~225 lines) into a dedicated UserControl with its own ViewModel.

## Files to Create

### 1. `BusLane/ViewModels/Core/NamespaceSelectionViewModel.cs`

A facade ViewModel that wraps existing state:

```csharp
public partial class NamespaceSelectionViewModel : ViewModelBase
{
    private readonly NavigationState _navigation;
    private readonly Action<ServiceBusNamespace> _onNamespaceSelected;

    [ObservableProperty] private bool _isOpen;

    // Delegate to NavigationState (no data duplication)
    public ObservableCollection<AzureSubscription> Subscriptions => _navigation.Subscriptions;
    public AzureSubscription? SelectedAzureSubscription
    {
        get => _navigation.SelectedAzureSubscription;
        set => _navigation.SelectedAzureSubscription = value;
    }
    public string NamespaceFilter
    {
        get => _navigation.NamespaceFilter;
        set => _navigation.NamespaceFilter = value;
    }
    public IEnumerable<ServiceBusNamespace> FilteredNamespaces => _navigation.FilteredNamespaces;
    public ObservableCollection<ServiceBusNamespace> Namespaces => _navigation.Namespaces;
    public ServiceBusNamespace? SelectedNamespace => _navigation.SelectedNamespace;

    public NamespaceSelectionViewModel(
        NavigationState navigation,
        Action<ServiceBusNamespace> onNamespaceSelected)
    {
        _navigation = navigation;
        _onNamespaceSelected = onNamespaceSelected;

        // Forward property changes from NavigationState
        _navigation.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(NavigationState.SelectedAzureSubscription)
                or nameof(NavigationState.NamespaceFilter)
                or nameof(NavigationState.FilteredNamespaces)
                or nameof(NavigationState.SelectedNamespace))
            {
                OnPropertyChanged(e.PropertyName);
            }
        };
    }

    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;

    [RelayCommand]
    private void SelectNamespace(ServiceBusNamespace ns)
    {
        _onNamespaceSelected(ns);
        Close();
    }
}
```

### 2. `BusLane/Views/Controls/NamespaceSelectionPanel.axaml`

Extract lines 298-522 from MainWindow.axaml into a self-contained UserControl:
- Includes modal overlay backdrop
- Includes slide-in panel with header, subscription picker, namespace list
- Manages own visibility via `IsOpen` binding

### 3. `BusLane/Views/Controls/NamespaceSelectionPanel.axaml.cs`

Standard code-behind with `InitializeComponent()`.

## Files to Modify

### `BusLane/ViewModels/MainWindowViewModel.cs`

```csharp
// Add property
public NamespaceSelectionViewModel NamespaceSelection { get; }

// In constructor
NamespaceSelection = new NamespaceSelectionViewModel(
    CurrentNavigation,
    SelectNamespaceAsync  // existing method or adapt
);

// Update commands to delegate
[RelayCommand]
private void CloseNamespacePanel() => NamespaceSelection.Close();

// Where OpenNamespacePanel is called, use:
NamespaceSelection.Open();
```

### `BusLane/ViewModels/Core/ConnectionViewModel.cs`

Remove:
- `_isNamespacePanelOpen` field
- `IsNamespacePanelOpen` property
- `OpenNamespacePanel()` method
- `CloseNamespacePanel()` method

### `BusLane/Views/MainWindow.axaml`

Replace lines 298-522 with:
```xml
<controls:NamespaceSelectionPanel
    Grid.Column="0" Grid.ColumnSpan="3"
    DataContext="{Binding NamespaceSelection}" />
```

Update any references from `Connection.IsNamespacePanelOpen` to use the new control.

## Impact

- **MainWindow.axaml:** ~609 lines → ~385 lines (37% reduction)
- **Breaking changes:** None - external behavior unchanged
- **Testability:** NamespaceSelectionViewModel can be unit tested with mocked NavigationState

## Future Extractions

Similar pattern can be applied to:
- Loading overlay (~60 lines)
- Live Stream/Charts overlay panels (~45 lines combined)
- Status bar (~30 lines)
- Unify Azure/ConnectionString content areas (~70 lines of near-duplication)
