namespace BusLane.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.Infrastructure;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public interface IReplayAuditStore
{
    Task<IReadOnlyList<ReplayAuditEntry>> LoadAsync(CancellationToken ct = default);
    Task AddAsync(ReplayAuditEntry entry, CancellationToken ct = default);
}

public sealed class ReplayAuditStore : IReplayAuditStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public ReplayAuditStore(string? path = null)
    {
        _path = path ?? AppPaths.ReplayAudit;
    }

    public async Task<IReadOnlyList<ReplayAuditEntry>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path, ct);
            return Deserialize<List<ReplayAuditEntry>>(json) ?? [];
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

    public async Task AddAsync(ReplayAuditEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _mutationLock.WaitAsync(ct);
        try
        {
            var entries = (await LoadAsync(ct)).ToList();
            entries.Add(entry);

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = Serialize(entries);
            await Task.Run(() => AppPaths.CreateSecureFile(_path, json), ct);
        }
        finally
        {
            _mutationLock.Release();
        }
    }
}
