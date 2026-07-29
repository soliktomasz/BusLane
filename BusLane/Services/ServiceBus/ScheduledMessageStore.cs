namespace BusLane.Services.ServiceBus;

using System.Text.Json;
using BusLane.Models;
using BusLane.Services.Infrastructure;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public interface IScheduledMessageStore
{
    Task<IReadOnlyList<ScheduledMessageIndexEntry>> LoadAsync(CancellationToken ct = default);
    Task AddAsync(
        ScheduledMessageIndexEntry entry,
        ScheduledMessagePayload? payload = null,
        CancellationToken ct = default);
    Task AddAsync(ScheduledMessageIndexEntry entry, CancellationToken ct);
    Task UpdateAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default);
    Task<ScheduledMessagePayload?> LoadPayloadAsync(
        ScheduledMessageIndexEntry entry,
        CancellationToken ct = default);
}

public class ScheduledMessageStore : IScheduledMessageStore
{
    private readonly string _path;
    private readonly IEncryptionService? _encryptionService;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public ScheduledMessageStore(string? path = null)
        : this(null, TimeProvider.System, path)
    {
    }

    public ScheduledMessageStore(
        IEncryptionService? encryptionService,
        TimeProvider timeProvider,
        string? path = null)
    {
        _encryptionService = encryptionService;
        _timeProvider = timeProvider;
        _path = path ?? AppPaths.ScheduledMessages;
    }

    public async Task<IReadOnlyList<ScheduledMessageIndexEntry>> LoadAsync(CancellationToken ct = default)
    {
        await _mutationLock.WaitAsync(ct);
        try
        {
            return await ReadCoreAsync(ct);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task AddAsync(
        ScheduledMessageIndexEntry entry,
        ScheduledMessagePayload? payload = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _mutationLock.WaitAsync(ct);
        try
        {
            var storedEntry = entry;
            if (payload is not null)
            {
                if (_encryptionService is null)
                {
                    throw new InvalidOperationException("Payload encryption is not configured.");
                }

                storedEntry = entry with
                {
                    SchemaVersion = ScheduledMessageIndexEntry.CurrentSchemaVersion,
                    EncryptedPayload = _encryptionService.Encrypt(Serialize(payload)),
                    BodyPreview = "",
                    SearchableProperties = entry.SearchableProperties.Keys.ToDictionary(
                        static key => key,
                        static _ => ""),
                    UpdatedAt = _timeProvider.GetUtcNow()
                };
            }

            var entries = (await ReadCoreAsync(ct)).ToList();
            entries.RemoveAll(e => string.Equals(e.RecordId, storedEntry.RecordId, StringComparison.Ordinal));
            entries.Add(storedEntry);
            await WriteCoreAsync(entries, ct);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public Task AddAsync(ScheduledMessageIndexEntry entry, CancellationToken ct) =>
        AddAsync(entry, null, ct);

    public async Task UpdateAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _mutationLock.WaitAsync(ct);
        try
        {
            var entries = (await ReadCoreAsync(ct)).ToList();
            var index = entries.FindIndex(e => string.Equals(e.RecordId, entry.RecordId, StringComparison.Ordinal));
            var updated = entry with { UpdatedAt = _timeProvider.GetUtcNow() };
            if (index >= 0)
            {
                entries[index] = updated;
            }
            else
            {
                entries.Add(updated);
            }

            await WriteCoreAsync(entries, ct);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<ScheduledMessagePayload?> LoadPayloadAsync(
        ScheduledMessageIndexEntry entry,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();
        if (_encryptionService is null || string.IsNullOrWhiteSpace(entry.EncryptedPayload))
        {
            return null;
        }

        try
        {
            var json = _encryptionService.Decrypt(entry.EncryptedPayload);
            return string.IsNullOrWhiteSpace(json) ? null : Deserialize<ScheduledMessagePayload>(json);
        }
        catch
        {
            return null;
        }
    }

    // Retained for compatibility with existing callers. New lifecycle code records resolution.
    public async Task RemoveAsync(string entityName, long sequenceNumber, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        await _mutationLock.WaitAsync(ct);
        try
        {
            var entries = (await ReadCoreAsync(ct)).ToList();
            entries.RemoveAll(e =>
                string.Equals(e.EntityName, entityName, StringComparison.OrdinalIgnoreCase) &&
                e.SequenceNumber == sequenceNumber);
            await WriteCoreAsync(entries, ct);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private async Task<IReadOnlyList<ScheduledMessageIndexEntry>> ReadCoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path, ct);
            using var document = JsonDocument.Parse(json);
            var entries = new List<ScheduledMessageIndexEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var entry = Deserialize<ScheduledMessageIndexEntry>(element.GetRawText());
                if (entry is null)
                {
                    continue;
                }

                if (!element.TryGetProperty(nameof(ScheduledMessageIndexEntry.SchemaVersion), out _))
                {
                    entry = entry with
                    {
                        SchemaVersion = 1,
                        RecordId = $"{entry.EntityName}:{entry.SequenceNumber}",
                        UpdatedAt = entry.UpdatedAt == default ? entry.CreatedAt : entry.UpdatedAt
                    };
                }

                if (entry.HasPayload && _encryptionService is not null)
                {
                    try
                    {
                        entry = entry with
                        {
                            IsPayloadUnavailable =
                                string.IsNullOrWhiteSpace(_encryptionService.Decrypt(entry.EncryptedPayload!))
                        };
                    }
                    catch
                    {
                        entry = entry with { IsPayloadUnavailable = true };
                    }
                }

                entries.Add(entry);
            }

            return entries;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("The scheduled message index could not be read.", ex);
        }
    }

    private async Task WriteCoreAsync(IReadOnlyList<ScheduledMessageIndexEntry> entries, CancellationToken ct)
    {
        var json = Serialize(entries);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await Task.Run(() => AppPaths.CreateSecureFile(_path, json), ct);
    }
}
