namespace BusLane.Services.Update;

using System.Text.Json;
using BusLane.Models.Update;
using Serilog;

public static class UpdateCheckService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/soliktomasz/BusLane/releases/latest";

    public static async Task<ReleaseInfo?> CheckForUpdateAsync(
        string currentVersion,
        string platform,
        HttpClient? httpClient = null)
    {
        var client = httpClient ?? new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "BusLane-AutoUpdater");

        try
        {
            Log.Information("Checking for updates from {Url}", GitHubApiUrl);
            var response = await client.GetStringAsync(GitHubApiUrl);
            var release = ParseGitHubRelease(response, platform);

            if (release == null)
            {
                Log.Warning("Failed to parse release info");
                return null;
            }

            if (!IsNewerVersion(currentVersion, release.Version))
            {
                Log.Information("Current version {Current} is up to date (latest: {Latest})",
                    currentVersion, release.Version);
                return null;
            }

            if (!release.Assets.ContainsKey(platform))
            {
                Log.Warning("No asset found for platform {Platform}", platform);
                return null;
            }

            Log.Information("Update available: {Version}", release.Version);
            return release;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for updates");
            return null;
        }
    }

    public static ReleaseInfo? ParseGitHubRelease(string json, string platform)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString();
            if (string.IsNullOrEmpty(tagName))
                return null;

            var version = tagName.TrimStart('v');
            var body = root.GetProperty("body").GetString() ?? string.Empty;
            var publishedAt = root.GetProperty("published_at").GetDateTime();
            var isPrerelease = root.GetProperty("prerelease").GetBoolean();

            var assets = new Dictionary<string, AssetInfo>();
            if (root.TryGetProperty("assets", out var assetsElement))
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (MatchesPlatform(name, platform))
                    {
                        assets[platform] = new AssetInfo
                        {
                            FileName = name,
                            DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty,
                            Size = asset.GetProperty("size").GetInt64(),
                            Checksum = string.Empty // GitHub doesn't provide checksums directly
                        };
                    }
                }
            }

            return new ReleaseInfo
            {
                Version = version,
                ReleaseNotes = body,
                PublishedAt = publishedAt,
                IsPrerelease = isPrerelease,
                Assets = assets
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse GitHub release JSON");
            return null;
        }
    }

    public static bool IsNewerVersion(string current, string latest)
    {
        try
        {
            var currentVersion = Version.Parse(current);
            var latestVersion = Version.Parse(latest);
            return latestVersion > currentVersion;
        }
        catch
        {
            // Fallback to string comparison for non-standard versions
            return string.Compare(latest, current, StringComparison.Ordinal) > 0;
        }
    }

    public static bool MatchesPlatform(string filename, string platform)
    {
        // Platform format: "win-x64", "osx-arm64", "linux-x64"
        var parts = platform.Split('-');
        if (parts.Length != 2)
            return false;

        var os = parts[0];
        var arch = parts[1];

        return os switch
        {
            "win" => filename.Contains("win") && filename.Contains(arch),
            "osx" => filename.Contains("osx") && filename.Contains(arch),
            "linux" => filename.Contains("linux") && filename.Contains(arch),
            _ => false
        };
    }

    public static string GetCurrentPlatform()
    {
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;

        var osPart = os.Contains("Windows") ? "win" :
                     os.Contains("Darwin") || os.Contains("Mac") ? "osx" :
                     os.Contains("Linux") ? "linux" : "unknown";

        var archPart = arch switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            _ => "x64"
        };

        return $"{osPart}-{archPart}";
    }
}
