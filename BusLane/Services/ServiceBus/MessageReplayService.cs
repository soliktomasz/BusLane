namespace BusLane.Services.ServiceBus;

using BusLane.Models;

public interface IReplayDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct = default);
}

public sealed class ReplayDelay : IReplayDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken ct = default)
    {
        return Task.Delay(duration, ct);
    }
}

public interface IMessageReplayService
{
    ReplayRequest CreateRequest(CorrelationMessage source, ReplayDestination destination);
    ReplayPreview Preview(ReplayRequest request);
    Task<ReplayResult> ReplayAsync(
        IServiceBusOperations operations,
        ReplayRequest request,
        CancellationToken ct = default);
}

public sealed class MessageReplayService : IMessageReplayService
{
    private readonly TimeProvider _timeProvider;
    private readonly IReplayDelay _delay;
    private readonly SemaphoreSlim _rateLock = new(1, 1);
    private DateTimeOffset? _lastReplayAt;

    public MessageReplayService(TimeProvider timeProvider, IReplayDelay delay)
    {
        _timeProvider = timeProvider;
        _delay = delay;
    }

    public ReplayRequest CreateRequest(CorrelationMessage source, ReplayDestination destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        return new ReplayRequest
        {
            Source = source,
            Destination = destination,
            Body = source.Body,
            ContentType = source.ContentType,
            CorrelationId = source.CorrelationId,
            MessageId = Guid.NewGuid().ToString(),
            SessionId = source.SessionId,
            Subject = source.Subject,
            To = source.To,
            ReplyTo = source.ReplyTo,
            ReplyToSessionId = source.ReplyToSessionId,
            PartitionKey = source.PartitionKey,
            TimeToLive = source.TimeToLive,
            Properties = source.Properties.ToDictionary(
                static property => property.Key,
                static property => property.Value)
        };
    }

    public ReplayPreview Preview(ReplayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Destination.EntityName))
        {
            errors.Add("Destination entity is required");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            errors.Add("Message body is required");
        }

        if (request.RateLimitPerSecond <= 0)
        {
            errors.Add("Rate limit must be greater than zero");
        }

        if (request.ScheduledEnqueueTime.HasValue &&
            request.ScheduledEnqueueTime.Value <= _timeProvider.GetUtcNow())
        {
            errors.Add("Scheduled enqueue time must be in the future");
        }

        if (request.Destination.RequiresSession && string.IsNullOrWhiteSpace(request.SessionId))
        {
            errors.Add("Session ID is required for the selected destination");
        }

        if (request.Properties.Keys.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Application property keys cannot be empty");
        }

        var warnings = new List<string>();
        if (request.Destination.Environment == ConnectionEnvironment.Production)
        {
            warnings.Add("Destination is tagged as Production");
        }

        return new ReplayPreview(errors, warnings, BuildChanges(request));
    }

    public async Task<ReplayResult> ReplayAsync(
        IServiceBusOperations operations,
        ReplayRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(request);

        var preview = Preview(request);
        if (!preview.IsValid)
        {
            return new ReplayResult(
                false,
                false,
                "Replay validation failed",
                ValidationErrors: preview.ValidationErrors);
        }

        if (!request.IsConfirmed)
        {
            return new ReplayResult(false, false, "Replay confirmation is required");
        }

        if (request.Destination.Environment == ConnectionEnvironment.Production &&
            !request.IsProductionAcknowledged)
        {
            return new ReplayResult(false, false, "Production replay acknowledgement is required");
        }

        await WaitForRateLimitAsync(request.RateLimitPerSecond, ct);

        try
        {
            var properties = request.Properties.ToDictionary(
                static property => property.Key,
                static property => property.Value);

            if (request.ScheduledEnqueueTime.HasValue)
            {
                var sequenceNumber = await operations.ScheduleMessageAsync(
                    request.Destination.EntityName,
                    request.Body,
                    properties,
                    request.ScheduledEnqueueTime.Value,
                    request.ContentType,
                    request.CorrelationId,
                    request.MessageId,
                    request.SessionId,
                    request.Subject,
                    request.To,
                    request.ReplyTo,
                    request.ReplyToSessionId,
                    request.PartitionKey,
                    request.TimeToLive,
                    ct);

                return new ReplayResult(
                    true,
                    true,
                    $"Message scheduled successfully (sequence {sequenceNumber})",
                    sequenceNumber);
            }

            await operations.SendMessageAsync(
                request.Destination.EntityName,
                request.Body,
                properties,
                request.ContentType,
                request.CorrelationId,
                request.MessageId,
                request.SessionId,
                request.Subject,
                request.To,
                request.ReplyTo,
                request.ReplyToSessionId,
                request.PartitionKey,
                request.TimeToLive,
                null,
                ct);

            return new ReplayResult(true, false, "Message replayed successfully");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ReplayResult(false, request.ScheduledEnqueueTime.HasValue, $"Replay failed: {ex.Message}");
        }
    }

    private async Task WaitForRateLimitAsync(int rateLimitPerSecond, CancellationToken ct)
    {
        var minimumInterval = TimeSpan.FromSeconds(1d / rateLimitPerSecond);
        await _rateLock.WaitAsync(ct);
        try
        {
            var now = _timeProvider.GetUtcNow();
            if (_lastReplayAt.HasValue)
            {
                var remaining = minimumInterval - (now - _lastReplayAt.Value);
                if (remaining > TimeSpan.Zero)
                {
                    await _delay.DelayAsync(remaining, ct);
                    now += remaining;
                }
            }

            _lastReplayAt = now;
        }
        finally
        {
            _rateLock.Release();
        }
    }

    private static IReadOnlyList<ReplayFieldChange> BuildChanges(ReplayRequest request)
    {
        var changes = new List<ReplayFieldChange>();
        AddChange(changes, "Destination", request.Source.EntityName, request.Destination.EntityName);
        AddChange(changes, "Body", request.Source.Body, request.Body);
        AddChange(changes, "ContentType", request.Source.ContentType, request.ContentType);
        AddChange(changes, "CorrelationId", request.Source.CorrelationId, request.CorrelationId);
        AddChange(changes, "MessageId", request.Source.MessageId, request.MessageId);
        AddChange(changes, "SessionId", request.Source.SessionId, request.SessionId);
        AddChange(changes, "Subject", request.Source.Subject, request.Subject);
        AddChange(changes, "To", request.Source.To, request.To);
        AddChange(changes, "ReplyTo", request.Source.ReplyTo, request.ReplyTo);
        AddChange(changes, "ReplyToSessionId", request.Source.ReplyToSessionId, request.ReplyToSessionId);
        AddChange(changes, "PartitionKey", request.Source.PartitionKey, request.PartitionKey);
        AddChange(changes, "TimeToLive", request.Source.TimeToLive?.ToString(), request.TimeToLive?.ToString());

        if (!PropertiesEqual(request.Source.Properties, request.Properties))
        {
            changes.Add(new ReplayFieldChange("ApplicationProperties", "Original", "Changed"));
        }

        if (request.ScheduledEnqueueTime.HasValue)
        {
            changes.Add(new ReplayFieldChange(
                "ScheduledEnqueueTime",
                null,
                request.ScheduledEnqueueTime.Value.ToString("O")));
        }

        return changes;
    }

    private static void AddChange(
        ICollection<ReplayFieldChange> changes,
        string field,
        string? sourceValue,
        string? replayValue)
    {
        if (!string.Equals(sourceValue, replayValue, StringComparison.Ordinal))
        {
            changes.Add(new ReplayFieldChange(field, sourceValue, replayValue));
        }
    }

    private static bool PropertiesEqual(
        IReadOnlyDictionary<string, object> source,
        IReadOnlyDictionary<string, object> replay)
    {
        return source.Count == replay.Count &&
               source.All(property =>
                   replay.TryGetValue(property.Key, out var value) &&
                   Equals(property.Value, value));
    }
}
