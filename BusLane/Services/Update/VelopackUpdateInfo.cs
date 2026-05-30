namespace BusLane.Services.Update;

/// <summary>Adapter-neutral details for a Velopack update.</summary>
public record VelopackUpdateInfo(
    string Version,
    string ReleaseNotes,
    DateTime? PublishedAt);
