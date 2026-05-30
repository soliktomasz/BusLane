namespace BusLane.Services.Update;

/// <summary>Testable boundary around Velopack update APIs.</summary>
public interface IVelopackUpdateManager
{
    /// <summary>Gets a value indicating whether the app is running from a Velopack installation.</summary>
    bool IsInstalled { get; }

    /// <summary>Gets the update that has been downloaded and is pending restart, if any.</summary>
    VelopackUpdateInfo? PendingUpdate { get; }

    /// <summary>Checks the configured update feed for a newer release.</summary>
    /// <param name="ct">A cancellation token for the update check.</param>
    /// <returns>A task returning update information when an update is available; otherwise, <see langword="null" />.</returns>
    Task<VelopackUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>Downloads the specified update and reports progress from the underlying Velopack operation.</summary>
    /// <param name="update">The update returned by <see cref="CheckForUpdatesAsync" />.</param>
    /// <param name="progress">Receives percentage progress updates during the download.</param>
    /// <param name="ct">A cancellation token for the download.</param>
    /// <returns>A task that completes when the update has been downloaded or cancellation is requested.</returns>
    Task DownloadUpdatesAsync(VelopackUpdateInfo update, Action<int> progress, CancellationToken ct = default);

    /// <summary>Applies the specified pending update and restarts the application.</summary>
    /// <param name="update">The update to apply. This must match the pending restart update.</param>
    void ApplyUpdatesAndRestart(VelopackUpdateInfo update);
}
