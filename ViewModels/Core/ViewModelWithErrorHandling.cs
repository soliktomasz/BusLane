using Azure.Messaging.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Base class for ViewModels with structured error handling.
/// Provides common busy state and status message management.
/// </summary>
public abstract partial class ViewModelWithErrorHandling : ViewModelBase
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _hasError;

    /// <summary>
    /// Executes an async action with standardized error handling.
    /// </summary>
    /// <param name="action">The async action to execute</param>
    /// <param name="loadingMessage">Message to display while loading</param>
    /// <param name="successMessage">Message to display on success (optional)</param>
    protected async Task ExecuteAsync(
        Func<Task> action,
        string? loadingMessage = null,
        string? successMessage = null)
    {
        if (IsBusy) return;

        IsBusy = true;
        HasError = false;
        ErrorMessage = null;
        StatusMessage = loadingMessage;

        try
        {
            await action();
            StatusMessage = successMessage;
        }
        catch (ServiceBusException sbEx)
        {
            HasError = true;
            ErrorMessage = FormatServiceBusError(sbEx);
            StatusMessage = ErrorMessage;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Unexpected error: {ex.Message}";
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Executes an async action with standardized error handling and a return value.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="action">The async action to execute</param>
    /// <param name="loadingMessage">Message to display while loading</param>
    /// <param name="successMessage">Message to display on success (optional)</param>
    /// <returns>The result of the action, or default if an error occurred</returns>
    protected async Task<T?> ExecuteAsync<T>(
        Func<Task<T>> action,
        string? loadingMessage = null,
        string? successMessage = null)
    {
        if (IsBusy) return default;

        IsBusy = true;
        HasError = false;
        ErrorMessage = null;
        StatusMessage = loadingMessage;

        try
        {
            var result = await action();
            StatusMessage = successMessage;
            return result;
        }
        catch (ServiceBusException sbEx)
        {
            HasError = true;
            ErrorMessage = FormatServiceBusError(sbEx);
            StatusMessage = ErrorMessage;
            return default;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Unexpected error: {ex.Message}";
            StatusMessage = ErrorMessage;
            return default;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Formats a ServiceBusException into a user-friendly error message.
    /// </summary>
    private static string FormatServiceBusError(ServiceBusException ex) =>
        ex.Reason switch
        {
            ServiceBusFailureReason.MessagingEntityNotFound =>
                "Entity not found. Ensure you have appropriate permissions.",
            ServiceBusFailureReason.ServiceBusy =>
                "Service is busy. Please try again later.",
            ServiceBusFailureReason.ServiceTimeout =>
                "Operation timed out. Please try again.",
            ServiceBusFailureReason.QuotaExceeded =>
                "Quota exceeded. Check your Service Bus namespace limits.",
            ServiceBusFailureReason.MessageSizeExceeded =>
                "Message size exceeded the allowed limit.",
            ServiceBusFailureReason.MessageLockLost =>
                "Message lock was lost. The message may have been processed by another receiver.",
            ServiceBusFailureReason.SessionLockLost =>
                "Session lock was lost. Please try again.",
            ServiceBusFailureReason.SessionCannotBeLocked =>
                "Session cannot be locked. It may be in use by another receiver.",
            _ => $"{ex.Reason}: {ex.Message}"
        };
}

