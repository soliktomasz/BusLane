namespace BusLane.Models.Update;

public record AssetInfo
{
    public string DownloadUrl { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public long Size { get; init; }
    public string Checksum { get; init; } = null!;
}
