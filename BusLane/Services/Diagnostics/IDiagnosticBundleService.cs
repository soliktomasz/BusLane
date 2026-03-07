namespace BusLane.Services.Diagnostics;

/// <summary>
/// Creates redacted support bundles for local troubleshooting.
/// </summary>
public interface IDiagnosticBundleService
{
    Task<string> ExportAsync(bool includeMessageBodies = false, CancellationToken ct = default);
}
