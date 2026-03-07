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
/// Stable identifier for a message in operation previews and results.
/// </summary>
public readonly record struct MessageIdentifier(long SequenceNumber, string? MessageId);

/// <summary>
/// Preview information for a bulk operation before execution.
/// </summary>
public record BulkOperationPreview(
    BulkOperationType OperationType,
    string ScopeDescription,
    int EstimatedImpactedCount,
    IReadOnlyList<string> SampleMessageIds,
    IReadOnlyList<string> Warnings,
    bool RequiresSession = false
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
    bool CanResume = false)
{
    public int FailedCount => RequestedCount - SucceededCount;

    public static BulkOperationExecutionResult Empty(BulkOperationType operationType, string summary) =>
        new(operationType, 0, 0, [], summary);
}
