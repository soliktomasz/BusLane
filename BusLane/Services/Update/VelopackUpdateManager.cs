namespace BusLane.Services.Update;

using Velopack;
using Velopack.Sources;

/// <summary>Velopack-backed update manager for the BusLane GitHub release feed.</summary>
public class VelopackUpdateManager : IVelopackUpdateManager
{
    private const string RepositoryUrl = "https://github.com/soliktomasz/BusLane";
    private readonly UpdateManager _manager;
    private UpdateInfo? _availableUpdate;

    public VelopackUpdateManager()
    {
        _manager = new UpdateManager(
            new GithubSource(RepositoryUrl, accessToken: null, prerelease: false, downloader: null));
    }

    public bool IsInstalled => _manager.IsInstalled;

    public VelopackUpdateInfo? PendingUpdate => _manager.UpdatePendingRestart is { } asset
        ? new VelopackUpdateInfo(
            asset.Version?.ToString() ?? string.Empty,
            asset.NotesMarkdown ?? string.Empty,
            null)
        : null;

    public async Task<VelopackUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        _availableUpdate = await _manager.CheckForUpdatesAsync();
        return _availableUpdate?.TargetFullRelease is { } asset
            ? new VelopackUpdateInfo(
                asset.Version?.ToString() ?? string.Empty,
                asset.NotesMarkdown ?? string.Empty,
                null)
            : null;
    }

    public Task DownloadUpdatesAsync(VelopackUpdateInfo update, Action<int> progress, CancellationToken ct = default)
    {
        if (_availableUpdate == null)
        {
            throw new InvalidOperationException("No Velopack update has been checked.");
        }

        return _manager.DownloadUpdatesAsync(_availableUpdate, progress, ct);
    }

    public void ApplyUpdatesAndRestart(VelopackUpdateInfo update)
    {
        var pending = _manager.UpdatePendingRestart;
        if (pending == null)
        {
            throw new InvalidOperationException("No Velopack update is ready to apply.");
        }

        _manager.ApplyUpdatesAndRestart(pending);
    }
}
