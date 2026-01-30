namespace BusLane.Models.Update;

public record ReleaseInfo
{
    public string Version { get; init; } = null!;
    public string ReleaseNotes { get; init; } = null!;
    public DateTime PublishedAt { get; init; }
    public Dictionary<string, AssetInfo> Assets { get; init; } = new();
    public bool IsPrerelease { get; init; }
}
