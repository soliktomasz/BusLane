namespace BusLane.Services.ServiceBus;

using System.Globalization;
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
            var operations = await ResolveOperationsAsync(entry);
            var stale = operations is null || entry.IsLegacyLimited;
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
        if (request.Entry.Environment == ConnectionEnvironment.Production &&
            !request.IsProductionAcknowledged)
        {
            return new(false, "Production acknowledgement is required", request.Entry);
        }

        var operations = await ResolveOperationsAsync(request.Entry);
        if (operations is null)
        {
            return new(false, "The indexed connection is unavailable", request.Entry);
        }

        try
        {
            await operations.CancelScheduledMessageAsync(
                request.Entry.EntityName, request.Entry.SequenceNumber, ct);
            var updated = request.Entry with
            {
                Status = ScheduledMessageRecordStatus.Cancelled,
                LastBrokerAction = "Cancel",
                LastBrokerActionAt = _timeProvider.GetUtcNow(),
                LastError = null
            };
            await _store.UpdateAsync(updated, ct);
            return new(true, "Scheduled message cancelled", updated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failed = request.Entry with
            {
                Status = ScheduledMessageRecordStatus.ActionFailed,
                LastBrokerAction = "Cancel",
                LastBrokerActionAt = _timeProvider.GetUtcNow(),
                LastError = ex.Message
            };
            await _store.UpdateAsync(failed, ct);
            return new(false, $"Cancellation failed: {ex.Message}", failed);
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

        var operations = await ResolveOperationsAsync(request.Entry);
        try
        {
            var properties = payload.Properties.ToDictionary(
                p => p.Key, p => ParseProperty(p.Value));
            var sequence = await operations!.ScheduleMessageAsync(
                request.Entry.EntityName, payload.Body, properties, request.NewScheduledTime.Value,
                payload.ContentType, payload.CorrelationId, payload.MessageId, payload.SessionId,
                payload.Subject, payload.To, payload.ReplyTo, payload.ReplyToSessionId,
                payload.PartitionKey, payload.TimeToLive, ct);
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
            await _store.AddAsync(replacement, payload, ct);
            var old = cancelled.Entry with
            {
                Status = ScheduledMessageRecordStatus.Rescheduled,
                ReplacementRecordId = replacement.RecordId,
                LastBrokerAction = "Reschedule"
            };
            await _store.UpdateAsync(old, ct);
            return new(true, "Scheduled message rescheduled", old);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var cancelledWithError = cancelled.Entry with { LastError = ex.Message };
            await _store.UpdateAsync(cancelledWithError, ct);
            return new(false,
                "The original schedule was cancelled, but the replacement could not be created.",
                cancelledWithError,
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
            var saved = await _connections.GetConnectionAsync(entry.ConnectionId);
            return saved is null ? null : _operationsFactory.CreateFromConnectionString(saved.ConnectionString);
        }

        if (!_auth.IsAuthenticated || _auth.Credential is null ||
            string.IsNullOrWhiteSpace(entry.NamespaceResourceId))
        {
            return null;
        }
        return _operationsFactory.CreateFromAzureCredential(
            entry.NamespaceEndpoint, entry.NamespaceResourceId, _auth.Credential);
    }

    private static object ParseProperty(ScheduledMessagePropertyValue property) => property.Type switch
    {
        nameof(Boolean) => bool.Parse(property.Value),
        nameof(Byte) => byte.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(SByte) => sbyte.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(Int16) => short.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(Int32) => int.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(Int64) => long.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(UInt16) => ushort.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(UInt32) => uint.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(UInt64) => ulong.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(Single) => float.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(Double) => double.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(Decimal) => decimal.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(Guid) => Guid.Parse(property.Value),
        nameof(DateTime) => DateTime.Parse(property.Value, CultureInfo.InvariantCulture),
        nameof(DateTimeOffset) => DateTimeOffset.Parse(property.Value, CultureInfo.InvariantCulture),
        _ => property.Value
    };
}
