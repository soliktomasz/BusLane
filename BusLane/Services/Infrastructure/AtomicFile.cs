namespace BusLane.Services.Infrastructure;

using Serilog;

/// <summary>
/// Replaces local files atomically by writing a sibling temporary file first.
/// </summary>
internal static class AtomicFile
{
    public static void WriteAllText(string path, string content, Action<string, string>? writeTemporaryFile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = Path.Combine(
            directory ?? string.Empty,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            (writeTemporaryFile ?? File.WriteAllText)(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Moves a file that could not be loaded to a timestamped .bak sibling, so the next save
    /// cannot overwrite the data it still holds.
    /// </summary>
    public static void PreserveUnreadable(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var backupPath = $"{path}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(path, backupPath, overwrite: true);
            Log.Warning("Could not load {Path}; kept its contents at {BackupPath}", path, backupPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load {Path} and could not back it up", path);
        }
    }
}
