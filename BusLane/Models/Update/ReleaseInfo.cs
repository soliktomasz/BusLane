namespace BusLane.Models.Update;

/// <summary>Describes a GitHub release available for update.</summary>
public record ReleaseInfo
{
    public string Version { get; init; } = null!;
    public string ReleaseNotes { get; init; } = null!;
    public DateTime PublishedAt { get; init; }
    public Dictionary<string, AssetInfo> Assets { get; init; } = new();
    public bool IsPrerelease { get; init; }
    public string ReleaseUrl { get; init; } = null!;
}
