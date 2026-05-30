namespace BusLane.Services.Update;

/// <summary>Testable boundary around Velopack update APIs.</summary>
public interface IVelopackUpdateManager
{
    bool IsInstalled { get; }
    VelopackUpdateInfo? PendingUpdate { get; }
    Task<VelopackUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default);
    Task DownloadUpdatesAsync(VelopackUpdateInfo update, Action<int> progress, CancellationToken ct = default);
    void ApplyUpdatesAndRestart(VelopackUpdateInfo update);
}
