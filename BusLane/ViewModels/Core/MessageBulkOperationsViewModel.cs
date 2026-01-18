using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Handles bulk message operations: purge, delete, resend, and resubmit dead letters.
/// </summary>
public partial class MessageBulkOperationsViewModel : ViewModelBase
{
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly NavigationState _navigation;
    private readonly IPreferencesService _preferencesService;
    private readonly Action<string> _setStatus;

    [ObservableProperty] private bool _isLoading;

    public MessageBulkOperationsViewModel(
        Func<IServiceBusOperations?> getOperations,
        NavigationState navigation,
        IPreferencesService preferencesService,
        Action<string> setStatus)
    {
        _getOperations = getOperations;
        _navigation = navigation;
        _preferencesService = preferencesService;
        _setStatus = setStatus;
    }

    /// <summary>
    /// Purges all messages from the current entity.
    /// </summary>
    public async Task<bool> ShouldConfirmPurgeAsync()
    {
        return _preferencesService.ConfirmBeforePurge;
    }

    public string GetPurgeConfirmationMessage()
    {
        var entityName = _navigation.CurrentEntityName ?? "";
        var subscription = _navigation.CurrentSubscriptionName;
        var queueType = _navigation.ShowDeadLetter ? "dead letter queue" : "queue";
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;
        return $"Are you sure you want to purge all messages from {queueType} of '{targetName}'? This action cannot be undone.";
    }

    public async Task ExecutePurgeAsync()
    {
        var operations = _getOperations();
        if (operations == null) return;

        var entityName = _navigation.CurrentEntityName;
        if (entityName == null) return;

        var subscription = _navigation.CurrentSubscriptionName;

        IsLoading = true;
        _setStatus("Purging messages...");

        try
        {
            await operations.PurgeMessagesAsync(entityName, subscription, _navigation.ShowDeadLetter);
            _setStatus("Purge complete");
        }
        catch (Exception ex)
        {
            _setStatus($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Resends multiple messages to the current entity.
    /// </summary>
    public Task<bool> ShouldConfirmBulkResendAsync()
    {
        return Task.FromResult(_preferencesService.ConfirmBeforePurge);
    }

    public string GetBulkResendConfirmationMessage(int count)
    {
        var entityName = _navigation.CurrentEntityName ?? "";
        return $"Are you sure you want to resend {count} message(s) to '{entityName}'?";
    }

    public async Task<int> ExecuteBulkResendAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0) return 0;

        var entityName = _navigation.CurrentEntityName;
        if (entityName == null) return 0;

        IsLoading = true;
        var count = selectedMessages.Count;
        _setStatus($"Resending {count} message(s)...");

        try
        {
            var messagesToResend = selectedMessages.ToList();
            var sentCount = await operations.ResendMessagesAsync(entityName, messagesToResend);

            _setStatus($"Successfully resent {sentCount} of {count} message(s)");
            return sentCount;
        }
        catch (Exception ex)
        {
            _setStatus($"Error resending messages: {ex.Message}");
            return 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Deletes multiple messages from the current entity.
    /// </summary>
    public string GetBulkDeleteConfirmationMessage(int count)
    {
        var entityName = _navigation.CurrentEntityName ?? "";
        var subscription = _navigation.CurrentSubscriptionName;
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;
        return $"Are you sure you want to delete {count} message(s) from '{targetName}'? This action cannot be undone.";
    }

    public async Task<int> ExecuteBulkDeleteAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0) return 0;

        var entityName = _navigation.CurrentEntityName;
        if (entityName == null) return 0;

        var subscription = _navigation.CurrentSubscriptionName;

        IsLoading = true;
        var count = selectedMessages.Count;
        _setStatus($"Deleting {count} message(s)...");

        try
        {
            var sequenceNumbers = selectedMessages.Select(m => m.SequenceNumber).ToList();
            var deletedCount = await operations.DeleteMessagesAsync(entityName, subscription, sequenceNumbers, _navigation.ShowDeadLetter);

            _setStatus($"Successfully deleted {deletedCount} of {count} message(s)");
            return deletedCount;
        }
        catch (Exception ex)
        {
            _setStatus($"Error deleting messages: {ex.Message}");
            return 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Resubmits dead letter messages back to the main entity.
    /// </summary>
    public string GetResubmitDeadLettersConfirmationMessage(int count)
    {
        var entityName = _navigation.CurrentEntityName ?? "";
        var subscription = _navigation.CurrentSubscriptionName;
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;
        return $"Are you sure you want to resubmit {count} message(s) from the dead letter queue back to '{targetName}'?";
    }

    public async Task<int> ExecuteResubmitDeadLettersAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0 || !_navigation.ShowDeadLetter) return 0;

        var entityName = _navigation.CurrentEntityName;
        if (entityName == null) return 0;

        var subscription = _navigation.CurrentSubscriptionName;

        IsLoading = true;
        var count = selectedMessages.Count;
        _setStatus($"Resubmitting {count} dead letter message(s)...");

        try
        {
            var messagesToResubmit = selectedMessages.ToList();
            var resubmittedCount = await operations.ResubmitDeadLetterMessagesAsync(entityName, subscription, messagesToResubmit);

            _setStatus($"Successfully resubmitted {resubmittedCount} of {count} message(s)");
            return resubmittedCount;
        }
        catch (Exception ex)
        {
            _setStatus($"Error resubmitting messages: {ex.Message}");
            return 0;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
