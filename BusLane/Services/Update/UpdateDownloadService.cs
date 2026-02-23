namespace BusLane.Services.Update;

using System.IO;
using System.Net;
using BusLane.Models.Update;
using Serilog;

public class UpdateDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly string _tempDirectory;

    public event EventHandler<double>? ProgressChanged;

    public UpdateDownloadService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "BusLane", "Updates");
    }

    public async Task<string?> DownloadAsync(AssetInfo asset, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_tempDirectory);

            var sanitizedFileName = Path.GetFileName(asset.FileName);
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                sanitizedFileName = "update.bin";
                Log.Warning("Asset filename was empty or contained only path separators, using default: {DefaultFileName}", sanitizedFileName);
            }

            var filePath = Path.Combine(_tempDirectory, sanitizedFileName);

            // Resume partial download if file exists
            var existingLength = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
            var totalBytes = asset.Size;

            if (existingLength >= totalBytes)
            {
                Log.Information("Update already downloaded: {File}", filePath);
                return filePath;
            }

            Log.Information("Downloading update from {Url} to {Path}", asset.DownloadUrl, filePath);

            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
            var isResuming = existingLength > 0;
            if (existingLength > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
                Log.Information("Resuming download from byte {Byte}", existingLength);
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (isResuming && response.StatusCode == HttpStatusCode.OK)
            {
                // Some servers ignore Range and return the full file with 200.
                // Restart the file to avoid appending full content to a partial file.
                Log.Warning("Server ignored range request; restarting download from the beginning");
                existingLength = 0;
            }

            response.EnsureSuccessStatusCode();

            var fileMode = existingLength > 0 ? FileMode.Append : FileMode.Create;
            using var fileStream = new FileStream(filePath, fileMode, FileAccess.Write, FileShare.None);
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[8192];
            var totalRead = existingLength;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;

                if (totalBytes > 0)
                {
                    var progress = (double)totalRead / totalBytes * 100;
                    ProgressChanged?.Invoke(this, progress);
                }
            }

            Log.Information("Download complete: {File} ({Bytes} bytes)", filePath, totalRead);
            return filePath;
        }
        catch (OperationCanceledException)
        {
            Log.Information("Download cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download update");
            return null;
        }
    }

    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                foreach (var file in Directory.GetFiles(_tempDirectory))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to cleanup download directory");
        }
    }
}
