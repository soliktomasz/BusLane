namespace BusLane.Models.Update;

/// <summary>Describes an available Velopack update.</summary>
public record ReleaseInfo
{
    public string Version { get; init; } = null!;
    public string ReleaseNotes { get; init; } = string.Empty;
    public DateTime? PublishedAt { get; init; }
    public bool CanSelfUpdate { get; init; } = true;
    public bool IsReadyToRestart { get; init; }
    public string ReleaseUrl { get; init; } = "https://github.com/soliktomasz/BusLane/releases";
}
