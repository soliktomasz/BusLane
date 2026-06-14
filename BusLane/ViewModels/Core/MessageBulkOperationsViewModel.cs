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
    [ObservableProperty] private int _bulkProgressCurrent;
    [ObservableProperty] private int _bulkProgressTotal;
    [ObservableProperty] private string _bulkProgressText = string.Empty;

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
    public Task<bool> ShouldConfirmPurgeAsync()
    {
        return Task.FromResult(true);
    }

    public async Task<string> GetPurgeConfirmationMessageAsync()
    {
        var preview = await BuildPurgePreviewAsync();
        var warnings = preview?.Warnings.Any() == true
            ? $"{Environment.NewLine}{Environment.NewLine}Warnings:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", preview.Warnings)}"
            : string.Empty;
        var context = preview?.Scope == null ? string.Empty : FormatScopeContext(preview.Scope);

        var strictWarning = _preferencesService.ConfirmBeforePurge
            ? $"{Environment.NewLine}{Environment.NewLine}Dry-run preview required. Purging messages cannot be undone."
            : $"{Environment.NewLine}{Environment.NewLine}Purging messages cannot be undone.";

        return preview == null
            ? "Are you sure you want to purge all messages? This action cannot be undone."
            : $"Dry-run preview{Environment.NewLine}Purge scope: {preview.ScopeDescription}{Environment.NewLine}Estimated messages: {preview.EstimatedImpactedCount}{context}{warnings}{strictWarning}";
    }

    public string GetPurgeConfirmText() =>
        _preferencesService.ConfirmBeforePurge ? "Confirm Purge" : "Purge";

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
        ResetProgress();
        _setStatus("Purging messages...");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.ServiceBus,
            LogLevel.Warning,
            $"Purging messages from {entityDisplay}..."));

        try
        {
            var result = await operations.PurgeMessagesDetailedAsync(
                entityName,
                subscription,
                _getNavigation().ShowDeadLetter,
                progress: CreateProgressReporter());
            _setStatus(result.FinalSummary);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"{result.FinalSummary} from {entityDisplay}"));
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
        return Task.FromResult(true);
    }

    public string GetBulkResendConfirmText() => "Resend";

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
        ResetProgress();
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
            var result = await operations.ResendMessagesDetailedAsync(
                entityName,
                messagesToResend,
                progress: CreateProgressReporter());

            _setStatus(result.FinalSummary);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"{result.FinalSummary} to {entityDisplay}"));
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

    public string GetBulkDeleteConfirmText() =>
        _preferencesService.ConfirmBeforeDelete ? "Confirm Delete" : "Delete";

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
        ResetProgress();
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
            var result = await operations.DeleteMessagesDetailedAsync(
                entityName,
                subscription,
                identifiers,
                _getNavigation().ShowDeadLetter,
                progress: CreateProgressReporter());

            _setStatus(result.FinalSummary);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"{result.FinalSummary} from {entityDisplay}"));
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
        ResetProgress();
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
            var result = await operations.ResubmitDeadLetterMessagesDetailedAsync(
                entityName,
                subscription,
                messagesToResubmit,
                progress: CreateProgressReporter());

            _setStatus(result.FinalSummary);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Info,
                $"{result.FinalSummary} to {entityDisplay}"));
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

        try
        {
            var preview = await operations.PreviewPurgeMessagesAsync(
                entityName,
                _getNavigation().CurrentSubscriptionName,
                _getNavigation().ShowDeadLetter);

            var nav = _getNavigation();
            return preview with
            {
                Scope = BuildScope(nav, preview.RequiresSession, []),
                IsHighRisk = true
            };
        }
        catch (Exception ex)
        {
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                "Failed to build purge preview",
                ex.Message));
            return null;
        }
    }

    private BulkOperationPreview BuildSelectionPreview(
        BulkOperationType operationType,
        IReadOnlyCollection<MessageInfo> selectedMessages,
        string verb)
    {
        var nav = _getNavigation();
        var requiresSession = nav.CurrentEntityRequiresSession || selectedMessages.Any(m => !string.IsNullOrWhiteSpace(m.SessionId));
        var sessionIds = selectedMessages
            .Select(m => m.SessionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var warnings = new List<string>();
        if (requiresSession)
        {
            warnings.Add("Selected messages span a session-enabled entity.");
        }

        if (nav.ShowDeadLetter && operationType == BulkOperationType.Delete)
        {
            warnings.Add("Deleting from the dead-letter queue is irreversible.");
        }

        var scope = BuildScope(nav, requiresSession, sessionIds);

        return new BulkOperationPreview(
            operationType,
            scope.DisplayName,
            selectedMessages.Count,
            selectedMessages.Select(m => m.MessageId).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().Take(5).ToList(),
            warnings,
            requiresSession,
            scope,
            operationType == BulkOperationType.Delete);
    }

    private static BulkOperationScope BuildScope(
        NavigationState nav,
        bool requiresSession,
        IReadOnlyList<string> sessionIds)
    {
        return new BulkOperationScope(
            nav.CurrentEntityName ?? "Unknown",
            nav.CurrentSubscriptionName,
            nav.ShowDeadLetter,
            requiresSession,
            sessionIds,
            BuildSelectedFilters(nav));
    }

    private static IReadOnlyDictionary<string, string> BuildSelectedFilters(NavigationState nav)
    {
        var filters = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(nav.EntityFilter))
        {
            filters["Entity filter"] = nav.EntityFilter;
        }

        if (!string.IsNullOrWhiteSpace(nav.NamespaceFilter))
        {
            filters["Namespace filter"] = nav.NamespaceFilter;
        }

        return filters;
    }

    private static string FormatPreview(BulkOperationPreview preview)
    {
        var sampleMessageIds = preview.SampleMessageIds.Any()
            ? $"{Environment.NewLine}Sample message IDs: {string.Join(", ", preview.SampleMessageIds)}"
            : string.Empty;
        var context = preview.Scope == null ? string.Empty : FormatScopeContext(preview.Scope);
        var warnings = preview.Warnings.Any()
            ? $"{Environment.NewLine}{Environment.NewLine}Warnings:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", preview.Warnings)}"
            : string.Empty;
        var highRisk = preview.IsHighRisk
            ? $"{Environment.NewLine}{Environment.NewLine}This action cannot be undone."
            : string.Empty;

        return $"Dry-run preview{Environment.NewLine}Scope: {preview.ScopeDescription}{Environment.NewLine}Estimated messages: {preview.EstimatedImpactedCount}{context}{sampleMessageIds}{warnings}{highRisk}";
    }

    private static string FormatScopeContext(BulkOperationScope scope)
    {
        var lines = new List<string>
        {
            $"Entity: {scope.EntityPath}",
            $"Message source: {(scope.IsDeadLetter ? "Dead-letter" : "Active")}",
            $"Sessions: {(scope.RequiresSession ? FormatSessions(scope.SessionIds) : "Not required")}"
        };

        if (scope.SelectedFilters.Any())
        {
            lines.Add($"Selected filters: {string.Join(", ", scope.SelectedFilters.Select(f => $"{f.Key}={f.Value}"))}");
        }

        return $"{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    private static string FormatSessions(IReadOnlyList<string> sessionIds)
    {
        return sessionIds.Count == 0
            ? "Session-enabled scope"
            : string.Join(", ", sessionIds);
    }

    private IProgress<BulkOperationProgress> CreateProgressReporter() =>
        new ImmediateBulkOperationProgress(ApplyProgress);

    private void ResetProgress()
    {
        BulkProgressCurrent = 0;
        BulkProgressTotal = 0;
        BulkProgressText = string.Empty;
    }

    private void ApplyProgress(BulkOperationProgress progress)
    {
        BulkProgressCurrent = progress.ProcessedCount;
        BulkProgressTotal = progress.RequestedCount;
        BulkProgressText = progress.Message;
        if (!string.IsNullOrWhiteSpace(progress.Message))
        {
            _setStatus(progress.Message);
        }
    }

    private sealed class ImmediateBulkOperationProgress(Action<BulkOperationProgress> onProgress) : IProgress<BulkOperationProgress>
    {
        public void Report(BulkOperationProgress value)
        {
            onProgress(value);
        }
    }
}
