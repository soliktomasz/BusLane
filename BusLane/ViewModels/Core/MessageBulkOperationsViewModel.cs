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

    public async Task<string> GetPurgeConfirmationMessageAsync()
    {
        var preview = await BuildPurgePreviewAsync();
        var warnings = preview?.Warnings.Any() == true
            ? $"{Environment.NewLine}{Environment.NewLine}Warnings:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", preview.Warnings)}"
            : string.Empty;

        return preview == null
            ? "Are you sure you want to purge all messages? This action cannot be undone."
            : $"Purge scope: {preview.ScopeDescription}{Environment.NewLine}Estimated messages: {preview.EstimatedImpactedCount}{warnings}";
    }

    public async Task ExecutePurgeAsync()
    {
        _ = await ExecutePurgeDetailedAsync();
    }

    public async Task<BulkOperationExecutionResult> ExecutePurgeDetailedAsync()
    {
        var operations = _getOperations();
        if (operations == null)
        {
            return BulkOperationExecutionResult.Empty(BulkOperationType.Purge, "No active connection");
        }

        var entityName = _getNavigation().CurrentEntityName;
        if (entityName == null)
        {
            return BulkOperationExecutionResult.Empty(BulkOperationType.Purge, "No entity selected");
        }

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
            var result = await operations.PurgeMessagesDetailedAsync(entityName, subscription, _getNavigation().ShowDeadLetter);
            _setStatus(result.Summary);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"{result.Summary} from {entityDisplay}"));
            return result;
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
            return BulkOperationExecutionResult.Empty(BulkOperationType.Purge, $"Error: {ex.Message}");
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

    public string GetBulkResendConfirmationMessage(ObservableCollection<MessageInfo> selectedMessages)
    {
        return FormatPreview(BuildBulkResendPreview(selectedMessages));
    }

    public async Task<int> ExecuteBulkResendAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var result = await ExecuteBulkResendDetailedAsync(selectedMessages);
        return result.SucceededCount;
    }

    public async Task<BulkOperationExecutionResult> ExecuteBulkResendDetailedAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0)
        {
            return BulkOperationExecutionResult.Empty(BulkOperationType.Resend, "No messages selected");
        }

        var entityName = _getNavigation().CurrentEntityName;
        if (entityName == null)
        {
            return BulkOperationExecutionResult.Empty(BulkOperationType.Resend, "No entity selected");
        }

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
            var result = await operations.ResendMessagesDetailedAsync(entityName, messagesToResend);

            _setStatus(result.Summary);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"{result.Summary} to {entityDisplay}"));
            return result;
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
            return BulkOperationExecutionResult.Empty(BulkOperationType.Resend, $"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Deletes multiple messages from the current entity.
    /// </summary>
    public string GetBulkDeleteConfirmationMessage(ObservableCollection<MessageInfo> selectedMessages)
    {
        return FormatPreview(BuildBulkDeletePreview(selectedMessages));
    }

    public async Task<int> ExecuteBulkDeleteAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var result = await ExecuteBulkDeleteDetailedAsync(selectedMessages);
        return result.SucceededCount;
    }

    public async Task<BulkOperationExecutionResult> ExecuteBulkDeleteDetailedAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0)
        {
            return BulkOperationExecutionResult.Empty(BulkOperationType.Delete, "No messages selected");
        }

        var entityName = _getNavigation().CurrentEntityName;
        if (entityName == null)
        {
            return BulkOperationExecutionResult.Empty(BulkOperationType.Delete, "No entity selected");
        }

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
            var identifiers = selectedMessages
                .Select(m => new MessageIdentifier(m.SequenceNumber, m.MessageId))
                .ToList();
            var result = await operations.DeleteMessagesDetailedAsync(entityName, subscription, identifiers, _getNavigation().ShowDeadLetter);

            _setStatus(result.Summary);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"{result.Summary} from {entityDisplay}"));
            return result;
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
            return BulkOperationExecutionResult.Empty(BulkOperationType.Delete, $"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Resubmits dead letter messages back to the main entity.
    /// </summary>
    public string GetResubmitDeadLettersConfirmationMessage(ObservableCollection<MessageInfo> selectedMessages)
    {
        return FormatPreview(BuildResubmitDeadLetterPreview(selectedMessages));
    }

    public async Task<int> ExecuteResubmitDeadLettersAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var result = await ExecuteResubmitDeadLettersDetailedAsync(selectedMessages);
        return result.SucceededCount;
    }

    public async Task<BulkOperationExecutionResult> ExecuteResubmitDeadLettersDetailedAsync(ObservableCollection<MessageInfo> selectedMessages)
    {
        var operations = _getOperations();
        if (operations == null || selectedMessages.Count == 0 || !_getNavigation().ShowDeadLetter)
        {
            return BulkOperationExecutionResult.Empty(BulkOperationType.ResubmitDeadLetter, "No dead-letter messages selected");
        }

        var entityName = _getNavigation().CurrentEntityName;
        if (entityName == null)
        {
            return BulkOperationExecutionResult.Empty(BulkOperationType.ResubmitDeadLetter, "No entity selected");
        }

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
            var result = await operations.ResubmitDeadLetterMessagesDetailedAsync(entityName, subscription, messagesToResubmit);

            _setStatus(result.Summary);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"{result.Summary} to {entityDisplay}"));
            return result;
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
            return BulkOperationExecutionResult.Empty(BulkOperationType.ResubmitDeadLetter, $"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public BulkOperationPreview BuildBulkDeletePreview(IReadOnlyCollection<MessageInfo> selectedMessages) =>
        BuildSelectionPreview(BulkOperationType.Delete, selectedMessages, "Delete");

    public BulkOperationPreview BuildBulkResendPreview(IReadOnlyCollection<MessageInfo> selectedMessages) =>
        BuildSelectionPreview(BulkOperationType.Resend, selectedMessages, "Resend");

    public BulkOperationPreview BuildResubmitDeadLetterPreview(IReadOnlyCollection<MessageInfo> selectedMessages) =>
        BuildSelectionPreview(BulkOperationType.ResubmitDeadLetter, selectedMessages, "Resubmit");

    public async Task<BulkOperationPreview?> BuildPurgePreviewAsync()
    {
        var operations = _getOperations();
        var entityName = _getNavigation().CurrentEntityName;
        if (operations == null || string.IsNullOrWhiteSpace(entityName))
        {
            return null;
        }

        return await operations.PreviewPurgeMessagesAsync(
            entityName,
            _getNavigation().CurrentSubscriptionName,
            _getNavigation().ShowDeadLetter);
    }

    private BulkOperationPreview BuildSelectionPreview(
        BulkOperationType operationType,
        IReadOnlyCollection<MessageInfo> selectedMessages,
        string verb)
    {
        var requiresSession = _getNavigation().CurrentEntityRequiresSession || selectedMessages.Any(m => !string.IsNullOrWhiteSpace(m.SessionId));
        var warnings = new List<string>();
        if (requiresSession)
        {
            warnings.Add("Selected messages span a session-enabled entity.");
        }

        if (_getNavigation().ShowDeadLetter && operationType == BulkOperationType.Delete)
        {
            warnings.Add("Deleting from the dead-letter queue is irreversible.");
        }

        return new BulkOperationPreview(
            operationType,
            GetEntityDisplayName(),
            selectedMessages.Count,
            selectedMessages.Select(m => m.MessageId).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().Take(5).ToList(),
            warnings,
            requiresSession);
    }

    private static string FormatPreview(BulkOperationPreview preview)
    {
        var sampleMessageIds = preview.SampleMessageIds.Any()
            ? $"{Environment.NewLine}Sample message IDs: {string.Join(", ", preview.SampleMessageIds)}"
            : string.Empty;
        var warnings = preview.Warnings.Any()
            ? $"{Environment.NewLine}{Environment.NewLine}Warnings:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", preview.Warnings)}"
            : string.Empty;

        return $"{preview.ScopeDescription}{Environment.NewLine}Estimated messages: {preview.EstimatedImpactedCount}{sampleMessageIds}{warnings}";
    }
}
