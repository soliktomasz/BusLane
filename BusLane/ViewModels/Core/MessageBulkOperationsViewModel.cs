namespace BusLane.ViewModels.Core;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Handles bulk message operations: purge, delete, resend, and resubmit dead letters.
/// </summary>
public partial class MessageBulkOperationsViewModel : ViewModelBase
{
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly Func<NavigationState> _getNavigation;
    private readonly IPreferencesService _preferencesService;
    private readonly ILogSink _logSink;
    private readonly Action<string> _setStatus;

    [ObservableProperty] private bool _isLoading;

    private string GetEntityDisplayName()
    {
        var nav = _getNavigation();
        var entityName = nav.CurrentEntityName ?? "Unknown";
        var subscription = nav.CurrentSubscriptionName;
        var dlq = nav.ShowDeadLetter ? " (DLQ)" : "";
        return subscription != null ? $"{entityName}/{subscription}{dlq}" : $"{entityName}{dlq}";
    }

    public MessageBulkOperationsViewModel(
        Func<IServiceBusOperations?> getOperations,
        Func<NavigationState> getNavigation,
        IPreferencesService preferencesService,
        ILogSink logSink,
        Action<string> setStatus)
    {
        _getOperations = getOperations;
        _getNavigation = getNavigation;
        _preferencesService = preferencesService;
        _logSink = logSink;
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
        var entityName = _getNavigation().CurrentEntityName ?? "";
        var subscription = _getNavigation().CurrentSubscriptionName;
        var queueType = _getNavigation().ShowDeadLetter ? "dead letter queue" : "queue";
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;
        return $"Are you sure you want to purge all messages from {queueType} of '{targetName}'? This action cannot be undone.";
    }

    public async Task ExecutePurgeAsync()
    {
        var operations = _getOperations();
        if (operations == null) return;

        var entityName = _getNavigation().CurrentEntityName;
        if (entityName == null) return;

        var subscription = _getNavigation().CurrentSubscriptionName;
        var entityDisplay = GetEntityDisplayName();

        IsLoading = true;
        _setStatus("Purging messages...");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.ServiceBus,
            LogLevel.Warning,
            $"Purging messages from {entityDisplay}..."));

        try
        {
            await operations.PurgeMessagesAsync(entityName, subscription, _getNavigation().ShowDeadLetter);
            _setStatus("Purge complete");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"Purged messages from {entityDisplay}"));
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to purge messages from {entityDisplay}";
            _setStatus($"Error: {ex.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                errorMsg,
                ex.Message));
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
        var entityName = _getNavigation().CurrentEntityName ?? "";
        return $"Are you sure you want to resend {count} message(s) to '{entityName}'?";
    }

    public async Task<int> ExecuteBulkResendAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0) return 0;

        var entityName = _getNavigation().CurrentEntityName;
        if (entityName == null) return 0;

        var entityDisplay = GetEntityDisplayName();

        IsLoading = true;
        var count = selectedMessages.Count;
        _setStatus($"Resending {count} message(s)...");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.ServiceBus,
            LogLevel.Info,
            $"Resending {count} message(s) to {entityDisplay}..."));

        try
        {
            var messagesToResend = selectedMessages.ToList();
            var sentCount = await operations.ResendMessagesAsync(entityName, messagesToResend);

            _setStatus($"Successfully resent {sentCount} of {count} message(s)");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"Resent {sentCount}/{count} messages to {entityDisplay}"));
            return sentCount;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to resend messages to {entityDisplay}";
            _setStatus($"Error resending messages: {ex.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                errorMsg,
                ex.Message));
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
        var entityName = _getNavigation().CurrentEntityName ?? "";
        var subscription = _getNavigation().CurrentSubscriptionName;
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;
        return $"Are you sure you want to delete {count} message(s) from '{targetName}'? This action cannot be undone.";
    }

    public async Task<int> ExecuteBulkDeleteAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0) return 0;

        var entityName = _getNavigation().CurrentEntityName;
        if (entityName == null) return 0;

        var subscription = _getNavigation().CurrentSubscriptionName;
        var entityDisplay = GetEntityDisplayName();

        IsLoading = true;
        var count = selectedMessages.Count;
        _setStatus($"Deleting {count} message(s)...");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.ServiceBus,
            LogLevel.Warning,
            $"Deleting {count} message(s) from {entityDisplay}..."));

        try
        {
            var sequenceNumbers = selectedMessages.Select(m => m.SequenceNumber).ToList();
            var deletedCount = await operations.DeleteMessagesAsync(entityName, subscription, sequenceNumbers, _getNavigation().ShowDeadLetter);

            _setStatus($"Successfully deleted {deletedCount} of {count} message(s)");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"Deleted {deletedCount}/{count} messages from {entityDisplay}"));
            return deletedCount;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to delete messages from {entityDisplay}";
            _setStatus($"Error deleting messages: {ex.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                errorMsg,
                ex.Message));
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
        var entityName = _getNavigation().CurrentEntityName ?? "";
        var subscription = _getNavigation().CurrentSubscriptionName;
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;
        return $"Are you sure you want to resubmit {count} message(s) from the dead letter queue back to '{targetName}'?";
    }

    public async Task<int> ExecuteResubmitDeadLettersAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0 || !_getNavigation().ShowDeadLetter) return 0;

        var entityName = _getNavigation().CurrentEntityName;
        if (entityName == null) return 0;

        var subscription = _getNavigation().CurrentSubscriptionName;
        var entityDisplay = GetEntityDisplayName();

        IsLoading = true;
        var count = selectedMessages.Count;
        _setStatus($"Resubmitting {count} dead letter message(s)...");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.ServiceBus,
            LogLevel.Info,
            $"Resubmitting {count} dead letter message(s) from {entityDisplay}..."));

        try
        {
            var messagesToResubmit = selectedMessages.ToList();
            var resubmittedCount = await operations.ResubmitDeadLetterMessagesAsync(entityName, subscription, messagesToResubmit);

            _setStatus($"Successfully resubmitted {resubmittedCount} of {count} message(s)");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"Resubmitted {resubmittedCount}/{count} dead letter messages to {entityDisplay}"));
            return resubmittedCount;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to resubmit dead letters to {entityDisplay}";
            _setStatus($"Error resubmitting messages: {ex.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                errorMsg,
                ex.Message));
            return 0;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
