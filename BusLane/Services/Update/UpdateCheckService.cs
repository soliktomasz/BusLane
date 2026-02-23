namespace BusLane.Services.Update;

using System.Text.Json;
using BusLane.Models.Update;
using Serilog;

public static class UpdateCheckService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/soliktomasz/BusLane/releases/latest";

    private static readonly HttpClient DefaultHttpClient = CreateDefaultHttpClient();

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("User-Agent", "BusLane-AutoUpdater");
        return client;
    }

    public static async Task<ReleaseInfo?> CheckForUpdateAsync(
        string currentVersion,
        string platform,
        HttpClient? httpClient = null)
    {
        var client = httpClient ?? DefaultHttpClient;

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
        catch (OperationCanceledException) when (client.Timeout == TimeSpan.FromSeconds(30))
        {
            Log.Warning("Update check timed out after 30 seconds");
            return null;
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
            var releaseUrl = root.TryGetProperty("html_url", out var htmlUrlElement)
                ? htmlUrlElement.GetString() ?? $"https://github.com/soliktomasz/BusLane/releases/tag/{tagName}"
                : $"https://github.com/soliktomasz/BusLane/releases/tag/{tagName}";

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
                Assets = assets,
                ReleaseUrl = releaseUrl
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
        if (TryParseSemanticVersion(current, out var currentSemanticVersion) &&
            TryParseSemanticVersion(latest, out var latestSemanticVersion))
        {
            return CompareSemanticVersions(latestSemanticVersion, currentSemanticVersion) > 0;
        }

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

    private static bool TryParseSemanticVersion(string value, out SemanticVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var withoutBuildMetadata = value.Split('+')[0];
        var parts = withoutBuildMetadata.Split('-', 2);
        var core = parts[0];
        var coreParts = core.Split('.');

        if (coreParts.Length != 3 ||
            !int.TryParse(coreParts[0], out var major) ||
            !int.TryParse(coreParts[1], out var minor) ||
            !int.TryParse(coreParts[2], out var patch) ||
            major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        string[] prereleaseIdentifiers = [];
        if (parts.Length == 2)
        {
            prereleaseIdentifiers = parts[1].Split('.');
            if (prereleaseIdentifiers.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }
        }

        version = new SemanticVersion(major, minor, patch, prereleaseIdentifiers);
        return true;
    }

    private static int CompareSemanticVersions(SemanticVersion left, SemanticVersion right)
    {
        var majorComparison = left.Major.CompareTo(right.Major);
        if (majorComparison != 0) return majorComparison;

        var minorComparison = left.Minor.CompareTo(right.Minor);
        if (minorComparison != 0) return minorComparison;

        var patchComparison = left.Patch.CompareTo(right.Patch);
        if (patchComparison != 0) return patchComparison;

        var leftHasPrerelease = left.PrereleaseIdentifiers.Length > 0;
        var rightHasPrerelease = right.PrereleaseIdentifiers.Length > 0;

        if (!leftHasPrerelease && !rightHasPrerelease) return 0;
        if (!leftHasPrerelease) return 1;
        if (!rightHasPrerelease) return -1;

        var minLength = Math.Min(left.PrereleaseIdentifiers.Length, right.PrereleaseIdentifiers.Length);
        for (var i = 0; i < minLength; i++)
        {
            var leftIdentifier = left.PrereleaseIdentifiers[i];
            var rightIdentifier = right.PrereleaseIdentifiers[i];

            var leftIsNumeric = int.TryParse(leftIdentifier, out var leftNumeric);
            var rightIsNumeric = int.TryParse(rightIdentifier, out var rightNumeric);

            if (leftIsNumeric && rightIsNumeric)
            {
                var numericComparison = leftNumeric.CompareTo(rightNumeric);
                if (numericComparison != 0) return numericComparison;
                continue;
            }

            if (leftIsNumeric != rightIsNumeric)
            {
                return leftIsNumeric ? -1 : 1;
            }

            var stringComparison = string.Compare(leftIdentifier, rightIdentifier, StringComparison.Ordinal);
            if (stringComparison != 0) return stringComparison;
        }

        return left.PrereleaseIdentifiers.Length.CompareTo(right.PrereleaseIdentifiers.Length);
    }

    private readonly record struct SemanticVersion(
        int Major,
        int Minor,
        int Patch,
        string[] PrereleaseIdentifiers);

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
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;

        var osPart = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                         System.Runtime.InteropServices.OSPlatform.Windows) ? "win" :
                     System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                         System.Runtime.InteropServices.OSPlatform.OSX) ? "osx" :
                     System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                         System.Runtime.InteropServices.OSPlatform.Linux) ? "linux" : "unknown";

        var archPart = arch switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            _ => "x64"
        };

        return $"{osPart}-{archPart}";
    }
}
