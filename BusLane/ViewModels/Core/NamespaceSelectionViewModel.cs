namespace BusLane.ViewModels.Core;

using System.Collections.ObjectModel;
using BusLane.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// ViewModel for the namespace selection slide-in panel.
/// Acts as a facade over NavigationState to provide a clean interface for the panel.
/// </summary>
public partial class NamespaceSelectionViewModel : ViewModelBase
{
    private readonly NavigationState _navigation;
    private readonly Func<ServiceBusNamespace, Task> _onNamespaceSelected;

    [ObservableProperty] private bool _isOpen;

    /// <summary>
    /// Gets the available Azure subscriptions.
    /// </summary>
    public ObservableCollection<AzureSubscription> Subscriptions => _navigation.Subscriptions;

    /// <summary>
    /// Gets or sets the currently selected Azure subscription.
    /// </summary>
    public AzureSubscription? SelectedAzureSubscription
    {
        get => _navigation.SelectedAzureSubscription;
        set => _navigation.SelectedAzureSubscription = value;
    }

    /// <summary>
    /// Gets or sets the namespace filter text.
    /// </summary>
    public string NamespaceFilter
    {
        get => _navigation.NamespaceFilter;
        set => _navigation.NamespaceFilter = value;
    }

    /// <summary>
    /// Gets the filtered namespaces based on current filter.
    /// </summary>
    public IEnumerable<ServiceBusNamespace> FilteredNamespaces => _navigation.FilteredNamespaces;

    /// <summary>
    /// Gets all namespaces (for count display).
    /// </summary>
    public ObservableCollection<ServiceBusNamespace> Namespaces => _navigation.Namespaces;

    /// <summary>
    /// Gets the currently selected namespace (for highlighting).
    /// </summary>
    public ServiceBusNamespace? SelectedNamespace => _navigation.SelectedNamespace;

    public NamespaceSelectionViewModel(
        NavigationState navigation,
        Func<ServiceBusNamespace, Task> onNamespaceSelected)
    {
        _navigation = navigation;
        _onNamespaceSelected = onNamespaceSelected;

        // Forward property changes from NavigationState
        _navigation.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(NavigationState.SelectedAzureSubscription):
                    OnPropertyChanged(nameof(SelectedAzureSubscription));
                    break;
                case nameof(NavigationState.NamespaceFilter):
                    OnPropertyChanged(nameof(NamespaceFilter));
                    break;
                case nameof(NavigationState.FilteredNamespaces):
                    OnPropertyChanged(nameof(FilteredNamespaces));
                    break;
                case nameof(NavigationState.SelectedNamespace):
                    OnPropertyChanged(nameof(SelectedNamespace));
                    break;
            }
        };
    }

    /// <summary>
    /// Opens the namespace selection panel.
    /// </summary>
    public void Open() => IsOpen = true;

    /// <summary>
    /// Closes the namespace selection panel.
    /// </summary>
    [RelayCommand]
    public void Close() => IsOpen = false;

    /// <summary>
    /// Selects a namespace and closes the panel.
    /// </summary>
    [RelayCommand]
    private async Task SelectNamespaceAsync(ServiceBusNamespace? ns)
    {
        if (ns == null) return;

        Close();
        await _onNamespaceSelected(ns);
    }
}
