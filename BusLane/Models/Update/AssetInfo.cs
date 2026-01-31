namespace BusLane.Models.Update;

/// <summary>Describes a downloadable asset attached to a GitHub release.</summary>
public record AssetInfo
{
    public string DownloadUrl { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public long Size { get; init; }
    public string Checksum { get; init; } = null!;
}
