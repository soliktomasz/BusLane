namespace BusLane.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.Auth;
using BusLane.Services.Storage;

public sealed record ScheduledMessageActionRequest(
    ScheduledMessageIndexEntry Entry,
    bool IsConfirmed,
    bool IsProductionAcknowledged,
    DateTimeOffset? NewScheduledTime = null);

public sealed record ScheduledMessageActionResult(
    bool IsSuccess,
    string Message,
    ScheduledMessageIndexEntry Entry,
    bool IsPartialFailure = false);

public sealed record ScheduledMessageResolvedEntry(
    ScheduledMessageIndexEntry Entry,
    string LocalState,
    bool IsStale);

public interface IScheduledMessageManagementService
{
    Task<IReadOnlyList<ScheduledMessageResolvedEntry>> RefreshAsync(CancellationToken ct = default);
    Task<ScheduledMessageActionResult> CancelAsync(ScheduledMessageActionRequest request, CancellationToken ct = default);
    Task<ScheduledMessageActionResult> RescheduleAsync(ScheduledMessageActionRequest request, CancellationToken ct = default);
    Task ResolveLocallyAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default);
    Task<ScheduledMessagePayload?> LoadPayloadAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default);
}

public sealed class ScheduledMessageManagementService : IScheduledMessageManagementService
{
    private readonly IConnectionStorageService _connections;
    private readonly IServiceBusOperationsFactory _operationsFactory;
    private readonly IAzureAuthService _auth;
    private readonly IScheduledMessageStore _store;
    private readonly TimeProvider _timeProvider;

    public ScheduledMessageManagementService(
        IConnectionStorageService connections,
        IServiceBusOperationsFactory operationsFactory,
        IAzureAuthService auth,
        IScheduledMessageStore store,
        TimeProvider timeProvider)
    {
        _connections = connections;
        _operationsFactory = operationsFactory;
        _auth = auth;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ScheduledMessageResolvedEntry>> RefreshAsync(CancellationToken ct = default)
    {
        var entries = await _store.LoadAsync(ct);
        var resolved = new List<ScheduledMessageResolvedEntry>(entries.Count);
        foreach (var entry in entries)
        {
            var stale = !await CanResolveConnectionAsync(entry) || entry.IsLegacyLimited;
            var localState = entry.Status switch
            {
                ScheduledMessageRecordStatus.Cancelled => "Cancelled (broker confirmed)",
                ScheduledMessageRecordStatus.Rescheduled => "Rescheduled (broker confirmed)",
                ScheduledMessageRecordStatus.ActionFailed => "Action failed",
                ScheduledMessageRecordStatus.ResolvedLocally => "Resolved locally",
                _ when stale => "Limited / stale",
                _ when entry.ScheduledEnqueueTime > _timeProvider.GetUtcNow() => "Upcoming (local)",
                _ => "Due / unverified (local)"
            };
            resolved.Add(new ScheduledMessageResolvedEntry(entry, localState, stale));
        }
        return resolved;
    }

    public async Task<ScheduledMessageActionResult> CancelAsync(
        ScheduledMessageActionRequest request,
        CancellationToken ct = default)
    {
        if (!request.IsConfirmed)
        {
            return new(false, "Cancellation confirmation is required", request.Entry);
        }
        SavedConnection? currentConnection;
        try
        {
            currentConnection = request.Entry.ConnectionKind == ScheduledMessageConnectionKind.ConnectionString
                ? await GetMatchingSavedConnectionAsync(request.Entry)
                : null;
        }
        catch (Exception ex)
        {
            return new(false, $"Connection resolution failed: {ex.Message}", request.Entry);
        }
        if (request.Entry.ConnectionKind == ScheduledMessageConnectionKind.ConnectionString &&
            currentConnection is null)
        {
            return new(false, "The indexed connection no longer matches its saved destination", request.Entry);
        }
        if ((request.Entry.Environment == ConnectionEnvironment.Production ||
             currentConnection?.Environment == ConnectionEnvironment.Production) &&
            !request.IsProductionAcknowledged)
        {
            return new(false, "Production acknowledgement is required", request.Entry);
        }

        IServiceBusOperations? operations;
        try
        {
            operations = currentConnection is not null
                ? _operationsFactory.CreateFromConnectionString(currentConnection.ConnectionString)
                : await ResolveOperationsAsync(request.Entry);
        }
        catch (Exception ex)
        {
            return new(false, $"Connection resolution failed: {ex.Message}", request.Entry);
        }
        if (operations is null)
        {
            return new(false, "The indexed connection is unavailable", request.Entry);
        }

        await using (operations)
        {
            try
            {
                await operations.CancelScheduledMessageAsync(
                    request.Entry.EntityName, request.Entry.SequenceNumber, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failed = request.Entry with
                {
                    Status = request.Entry.IsBrokerConfirmed
                        ? request.Entry.Status
                        : ScheduledMessageRecordStatus.ActionFailed,
                    LastBrokerAction = "Cancel",
                    LastBrokerActionAt = _timeProvider.GetUtcNow(),
                    LastError = ex.Message
                };
                await _store.UpdateAsync(failed, ct);
                return new(false, $"Cancellation failed: {ex.Message}", failed);
            }
        }

        var updated = request.Entry with
        {
            Status = ScheduledMessageRecordStatus.Cancelled,
            LastBrokerAction = "Cancel",
            LastBrokerActionAt = _timeProvider.GetUtcNow(),
            LastError = null
        };
        try
        {
            await _store.UpdateAsync(updated, ct);
            return new(true, "Scheduled message cancelled", updated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(true,
                "Scheduled message cancelled. The local schedule index could not be updated.",
                updated,
                IsPartialFailure: true);
        }
    }

    public async Task<ScheduledMessageActionResult> RescheduleAsync(
        ScheduledMessageActionRequest request,
        CancellationToken ct = default)
    {
        if (request.NewScheduledTime is null || request.NewScheduledTime <= _timeProvider.GetUtcNow())
        {
            return new(false, "A future scheduled time is required", request.Entry);
        }

        var payload = await _store.LoadPayloadAsync(request.Entry, ct);
        if (payload is null)
        {
            return new(false, "The scheduled payload is unavailable", request.Entry);
        }

        var cancelled = await CancelAsync(request, ct);
        if (!cancelled.IsSuccess)
        {
            return cancelled;
        }

        IServiceBusOperations? operations;
        try
        {
            operations = await ResolveOperationsAsync(request.Entry);
        }
        catch (Exception ex)
        {
            return new(false,
                $"The original schedule was cancelled, but connection resolution failed: {ex.Message}",
                cancelled.Entry with { LastError = ex.Message },
                IsPartialFailure: true);
        }
        if (operations is null)
        {
            return new(false, "The indexed connection is unavailable", cancelled.Entry, true);
        }
        long sequence;
        await using (operations)
        try
        {
            var properties = payload.Properties.ToDictionary(
                p => p.Key, p => p.Value.ToObject()!);
            sequence = await operations.ScheduleMessageAsync(
                request.Entry.EntityName, payload.Body, properties, request.NewScheduledTime.Value,
                payload.ContentType, payload.CorrelationId, payload.MessageId, payload.SessionId,
                payload.Subject, payload.To, payload.ReplyTo, payload.ReplyToSessionId,
                payload.PartitionKey, payload.TimeToLive, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var cancelledWithError = cancelled.Entry with { LastError = ex.Message };
            try
            {
                await _store.UpdateAsync(cancelledWithError, ct);
            }
            catch
            {
                // Broker-confirmed cancellation remains the primary outcome.
            }
            return new(false,
                "The original schedule was cancelled, but the replacement could not be created.",
                cancelledWithError,
                IsPartialFailure: true);
        }

        var now = _timeProvider.GetUtcNow();
        var replacement = request.Entry with
        {
            RecordId = Guid.NewGuid().ToString("N"),
            ReplacementRecordId = null,
            SequenceNumber = sequence,
            ScheduledEnqueueTime = request.NewScheduledTime.Value,
            CreatedAt = now,
            UpdatedAt = now,
            Status = ScheduledMessageRecordStatus.Indexed,
            LastBrokerAction = null,
            LastBrokerActionAt = null,
            LastError = null,
            EncryptedPayload = null
        };
        var old = cancelled.Entry with
        {
            Status = ScheduledMessageRecordStatus.Rescheduled,
            ReplacementRecordId = replacement.RecordId,
            LastBrokerAction = "Reschedule"
        };
        try
        {
            await _store.AddAsync(replacement, payload, ct);
            await _store.UpdateAsync(old, ct);
            return new(true, "Scheduled message rescheduled", old);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(true,
                "Replacement scheduled successfully. The local schedule index could not be updated.",
                cancelled.Entry,
                IsPartialFailure: true);
        }
    }

    public Task ResolveLocallyAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default) =>
        _store.UpdateAsync(entry with
        {
            Status = ScheduledMessageRecordStatus.ResolvedLocally,
            LastBrokerAction = null,
            LastBrokerActionAt = null
        }, ct);

    public Task<ScheduledMessagePayload?> LoadPayloadAsync(
        ScheduledMessageIndexEntry entry,
        CancellationToken ct = default) => _store.LoadPayloadAsync(entry, ct);

    private async Task<IServiceBusOperations?> ResolveOperationsAsync(ScheduledMessageIndexEntry entry)
    {
        if (entry.ConnectionKind == ScheduledMessageConnectionKind.ConnectionString)
        {
            var saved = await GetMatchingSavedConnectionAsync(entry);
            return saved is null ? null : _operationsFactory.CreateFromConnectionString(saved.ConnectionString);
        }

        if (!_auth.IsAuthenticated || _auth.Credential is null ||
            string.IsNullOrWhiteSpace(entry.NamespaceResourceId) ||
            !NamespaceIdentityMatches(entry))
        {
            return null;
        }
        return _operationsFactory.CreateFromAzureCredential(
            entry.NamespaceEndpoint, entry.NamespaceResourceId, _auth.Credential);
    }

    private async Task<bool> CanResolveConnectionAsync(ScheduledMessageIndexEntry entry)
    {
        if (entry.ConnectionKind == ScheduledMessageConnectionKind.AzureCredential)
        {
            return _auth.IsAuthenticated && _auth.Credential is not null && NamespaceIdentityMatches(entry);
        }
        if (!string.IsNullOrWhiteSpace(entry.ConnectionId))
        {
            return await GetMatchingSavedConnectionAsync(entry) is not null;
        }
        var matches = (await _connections.GetConnectionsAsync())
            .Count(connection => connection.IsNamespaceLevel ||
                                 string.Equals(connection.EntityName, entry.EntityName,
                                     StringComparison.OrdinalIgnoreCase));
        return matches == 1;
    }

    private async Task<SavedConnection?> GetMatchingSavedConnectionAsync(ScheduledMessageIndexEntry entry)
    {
        SavedConnection? saved;
        if (!string.IsNullOrWhiteSpace(entry.ConnectionId))
        {
            saved = await _connections.GetConnectionAsync(entry.ConnectionId);
        }
        else
        {
            saved = (await _connections.GetConnectionsAsync())
                .Where(connection => connection.IsNamespaceLevel ||
                                     string.Equals(connection.EntityName, entry.EntityName,
                                         StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray() is [var only] ? only : null;
        }

        if (saved is null)
        {
            return null;
        }
        if (!saved.IsNamespaceLevel &&
            !string.Equals(saved.EntityName, entry.EntityName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (!string.IsNullOrWhiteSpace(entry.NamespaceEndpoint) &&
            !string.Equals(
                NormalizeEndpoint(saved.Endpoint),
                NormalizeEndpoint(entry.NamespaceEndpoint),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return saved;
    }

    private static string NormalizeEndpoint(string? endpoint) =>
        (endpoint ?? "").Replace("sb://", "", StringComparison.OrdinalIgnoreCase).TrimEnd('/');

    private static bool NamespaceIdentityMatches(ScheduledMessageIndexEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.NamespaceResourceId) ||
            string.IsNullOrWhiteSpace(entry.NamespaceEndpoint))
        {
            return false;
        }
        var namespaceName = entry.NamespaceEndpoint
            .Replace("sb://", "", StringComparison.OrdinalIgnoreCase)
            .Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        return entry.NamespaceResourceId.Contains(
            $"/namespaces/{namespaceName}", StringComparison.OrdinalIgnoreCase);
    }

}
