namespace BusLane.Services.Infrastructure;

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
}
