namespace BusLane.Models;

/// <summary>
/// Supported bulk operation kinds.
/// </summary>
public enum BulkOperationType
{
    Purge,
    Delete,
    Resend,
    ResubmitDeadLetter
}

/// <summary>
/// Classifies whether a failed bulk-operation item can reasonably be retried.
/// </summary>
public enum BulkOperationFailureKind
{
    Retryable,
    NonRetryable
}

/// <summary>
/// Describes the final state of a bulk operation.
/// </summary>
public enum BulkOperationCompletionStatus
{
    Succeeded,
    PartialFailure,
    Failed,
    Cancelled
}

/// <summary>
/// Stable identifier for a message in operation previews and results.
/// </summary>
public readonly record struct MessageIdentifier(long SequenceNumber, string? MessageId, string? SessionId = null);

/// <summary>
/// Entity, DLQ, session, and filter context for a bulk operation preview.
/// </summary>
public record BulkOperationScope(
    string EntityName,
    string? SubscriptionName,
    bool IsDeadLetter,
    bool RequiresSession,
    IReadOnlyList<string> SessionIds,
    IReadOnlyDictionary<string, string> SelectedFilters)
{
    public string EntityPath => SubscriptionName == null ? EntityName : $"{EntityName}/{SubscriptionName}";
    public string DisplayName => IsDeadLetter ? $"{EntityPath} (DLQ)" : EntityPath;
}

/// <summary>
/// Progress update for long-running bulk operations.
/// </summary>
public record BulkOperationProgress(
    BulkOperationType OperationType,
    int ProcessedCount,
    int RequestedCount,
    string Message)
{
    public double PercentComplete => RequestedCount <= 0 ? 0 : Math.Min(100, ProcessedCount * 100d / RequestedCount);
}

/// <summary>
/// Classified failure for one message in a bulk operation.
/// </summary>
public record BulkOperationFailure(
    MessageIdentifier Identifier,
    BulkOperationFailureKind Kind,
    string Reason);

/// <summary>
/// Preview information for a bulk operation before execution.
/// </summary>
public record BulkOperationPreview(
    BulkOperationType OperationType,
    string ScopeDescription,
    int EstimatedImpactedCount,
    IReadOnlyList<string> SampleMessageIds,
    IReadOnlyList<string> Warnings,
    bool RequiresSession = false,
    BulkOperationScope? Scope = null,
    bool IsHighRisk = false
);

/// <summary>
/// Detailed result for a bulk operation execution.
/// </summary>
public record BulkOperationExecutionResult(
    BulkOperationType OperationType,
    int RequestedCount,
    int SucceededCount,
    IReadOnlyList<MessageIdentifier> FailedIdentifiers,
    string Summary,
    bool CanResume = false,
    IReadOnlyList<BulkOperationFailure>? Failures = null,
    BulkOperationCompletionStatus? CompletionStatus = null)
{
    public int FailedCount => RequestedCount - SucceededCount;
    public IReadOnlyList<BulkOperationFailure> ClassifiedFailures => Failures ?? FailedIdentifiers
        .Select(identifier => new BulkOperationFailure(identifier, BulkOperationFailureKind.Retryable, "Operation did not complete for this message"))
        .ToList();
    public int RetryableFailureCount => ClassifiedFailures.Count(f => f.Kind == BulkOperationFailureKind.Retryable);
    public int NonRetryableFailureCount => ClassifiedFailures.Count(f => f.Kind == BulkOperationFailureKind.NonRetryable);
    public BulkOperationCompletionStatus Status => CompletionStatus ?? InferStatus();
    public string FinalSummary => BuildFinalSummary();

    public static BulkOperationExecutionResult Empty(BulkOperationType operationType, string summary) =>
        new(operationType, 0, 0, [], summary);

    private BulkOperationCompletionStatus InferStatus()
    {
        if (RequestedCount > 0 && SucceededCount == 0 && FailedCount > 0)
        {
            return BulkOperationCompletionStatus.Failed;
        }

        return FailedCount > 0
            ? BulkOperationCompletionStatus.PartialFailure
            : BulkOperationCompletionStatus.Succeeded;
    }

    private string BuildFinalSummary()
    {
        if (Status == BulkOperationCompletionStatus.Succeeded)
        {
            return Summary;
        }

        var statusText = Status switch
        {
            BulkOperationCompletionStatus.Cancelled => "Cancelled",
            BulkOperationCompletionStatus.Failed => "Failed",
            BulkOperationCompletionStatus.PartialFailure => "Partial failure",
            _ => "Completed"
        };

        if (ClassifiedFailures.Count == 0)
        {
            return $"{Summary}. {statusText}.";
        }

        return $"{Summary}. {statusText}. Retryable: {RetryableFailureCount}. Non-retryable: {NonRetryableFailureCount}.";
    }
}
