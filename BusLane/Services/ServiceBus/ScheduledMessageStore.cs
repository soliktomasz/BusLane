namespace BusLane.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.Infrastructure;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public interface IScheduledMessageStore
{
    Task<IReadOnlyList<ScheduledMessageIndexEntry>> LoadAsync(CancellationToken ct = default);
    Task AddAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default);
    Task RemoveAsync(string entityName, long sequenceNumber, CancellationToken ct = default);
}

public class ScheduledMessageStore : IScheduledMessageStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public ScheduledMessageStore(string? path = null)
    {
        _path = path ?? AppPaths.ScheduledMessages;
    }

    public async Task<IReadOnlyList<ScheduledMessageIndexEntry>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path, ct);
            return Deserialize<List<ScheduledMessageIndexEntry>>(json) ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    public async Task AddAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _mutationLock.WaitAsync(ct);
        try
        {
            var entries = (await LoadAsync(ct)).ToList();
            entries.RemoveAll(e => string.Equals(e.EntityName, entry.EntityName, StringComparison.OrdinalIgnoreCase) &&
                                   e.SequenceNumber == entry.SequenceNumber);
            entries.Add(entry);
            await SaveAsync(entries, ct);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task RemoveAsync(string entityName, long sequenceNumber, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        await _mutationLock.WaitAsync(ct);
        try
        {
            var entries = (await LoadAsync(ct)).ToList();
            entries.RemoveAll(e => string.Equals(e.EntityName, entityName, StringComparison.OrdinalIgnoreCase) &&
                                   e.SequenceNumber == sequenceNumber);
            await SaveAsync(entries, ct);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private async Task SaveAsync(IReadOnlyList<ScheduledMessageIndexEntry> entries, CancellationToken ct)
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
