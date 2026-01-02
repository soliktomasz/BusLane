using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.Services.Abstractions;

/// <summary>
/// Interface for managing dialog operations.
/// </summary>
public interface IDialogService
{
    bool ShowConfirmDialog { get; }
    string Title { get; }
    string Message { get; }
    string ConfirmText { get; }

    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Confirm");
    void Confirm();
    void Cancel();
}

/// <summary>
/// Service for managing confirmation dialogs.
/// Provides a clean API for showing dialogs and awaiting user response.
/// </summary>
public partial class DialogService : ObservableObject, IDialogService
{
    [ObservableProperty] private bool _showConfirmDialog;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _confirmText = "Confirm";

    private TaskCompletionSource<bool>? _dialogResult;

    /// <summary>
    /// Shows a confirmation dialog and awaits the user's response.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    /// <param name="confirmText">Text for the confirm button</param>
    /// <returns>True if confirmed, false if cancelled</returns>
    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirm")
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;

        _dialogResult = new TaskCompletionSource<bool>();
        ShowConfirmDialog = true;

        return await _dialogResult.Task;
    }

    [RelayCommand]
    public void Confirm()
    {
        ShowConfirmDialog = false;
        _dialogResult?.TrySetResult(true);
    }

    [RelayCommand]
    public void Cancel()
    {
        ShowConfirmDialog = false;
        _dialogResult?.TrySetResult(false);
    }
}

