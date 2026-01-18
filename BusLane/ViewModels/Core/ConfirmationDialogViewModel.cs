using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Manages confirmation dialog state and actions.
/// </summary>
public partial class ConfirmationDialogViewModel : ViewModelBase
{
    private Func<Task>? _confirmAction;

    [ObservableProperty] private bool _showConfirmDialog;
    [ObservableProperty] private string _confirmDialogTitle = "";
    [ObservableProperty] private string _confirmDialogMessage = "";
    [ObservableProperty] private string _confirmDialogConfirmText = "Confirm";

    public void ShowConfirmation(string title, string message, string confirmText, Func<Task> action)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogMessage = message;
        ConfirmDialogConfirmText = confirmText;
        _confirmAction = action;
        ShowConfirmDialog = true;
    }

    public void CloseConfirmation()
    {
        ShowConfirmDialog = false;
        _confirmAction = null;
    }

    public async Task ExecuteConfirmDialogAsync()
    {
        ShowConfirmDialog = false;
        if (_confirmAction != null)
        {
            await _confirmAction();
            _confirmAction = null;
        }
    }

    public void CancelConfirmDialog()
    {
        ShowConfirmDialog = false;
        _confirmAction = null;
    }
}
