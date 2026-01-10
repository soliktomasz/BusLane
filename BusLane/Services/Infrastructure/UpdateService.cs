using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BusLane.Services.Abstractions;

namespace BusLane.Services.Infrastructure;

/// <summary>
/// Service for checking and managing application updates from GitHub Releases.
/// </summary>
public class UpdateService : IUpdateService
{
    private const string GitHubApiBaseUrl = "https://api.github.com";
    private const string RepositoryOwner = "soliktomasz";
    private const string RepositoryName = "BusLane";
    private const string UserAgent = "BusLane-UpdateChecker/1.0";

    private readonly HttpClient _httpClient;
    private readonly IVersionService _versionService;
    private readonly string _cacheFilePath;

    private ReleaseInfo? _latestReleaseInfo;
    private DateTime? _lastCheckTime;
    private bool _isUpdateAvailable;

    public bool IsUpdateAvailable => _isUpdateAvailable;
    public ReleaseInfo? LatestReleaseInfo => _latestReleaseInfo;
    public DateTime? LastCheckTime => _lastCheckTime;

    public UpdateService(IVersionService versionService)
    {
        _versionService = versionService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

        _cacheFilePath = Path.Combine(AppPaths.AppDataDirectory, "update_cache.json");
        LoadCachedInfo();
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            var releaseInfo = await GetLatestReleaseInfoAsync();
            if (releaseInfo == null)
            {
                return false;
            }

            var currentVersion = ParseVersion(_versionService.InformationalVersion);
            var latestVersion = ParseVersion(releaseInfo.Version);

            if (currentVersion == null || latestVersion == null)
            {
                return false;
            }

            _isUpdateAvailable = CompareVersions(currentVersion, latestVersion) < 0;
            _lastCheckTime = DateTime.UtcNow;
            _latestReleaseInfo = releaseInfo;

            SaveCachedInfo();
            return _isUpdateAvailable;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - update checks should not crash the app
            Debug.WriteLine($"Error checking for updates: {ex.Message}");
            return false;
        }
    }

    public async Task<ReleaseInfo?> GetLatestReleaseInfoAsync()
    {
        try
        {
            var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

            var root = jsonDoc.RootElement;
            var tagName = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var version = tagName.TrimStart('v'); // Remove 'v' prefix if present
            var releaseNotes = root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty;
            var downloadUrl = root.TryGetProperty("html_url", out var htmlUrl) ? htmlUrl.GetString() ?? string.Empty : string.Empty;
            var publishedAt = root.TryGetProperty("published_at", out var published) && published.GetString() != null
                ? DateTime.Parse(published.GetString()!)
                : DateTime.UtcNow;
            var isPrerelease = root.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean();

            var platformUrls = new Dictionary<string, string>();
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("browser_download_url", out var downloadUrlProp) &&
                        asset.TryGetProperty("name", out var nameProp))
                    {
                        var assetName = nameProp.GetString() ?? string.Empty;
                        var assetUrl = downloadUrlProp.GetString() ?? string.Empty;

                        // Map asset names to platforms
                        if (assetName.Contains("osx-arm64") || assetName.Contains("macos-arm64"))
                        {
                            platformUrls["osx-arm64"] = assetUrl;
                        }
                        else if (assetName.Contains("osx-x64") || assetName.Contains("macos-x64"))
                        {
                            platformUrls["osx-x64"] = assetUrl;
                        }
                        else if (assetName.Contains("win-x64") || assetName.Contains("windows"))
                        {
                            platformUrls["win-x64"] = assetUrl;
                        }
                        else if (assetName.Contains("linux-x64") || assetName.Contains("linux"))
                        {
                            platformUrls["linux-x64"] = assetUrl;
                        }
                    }
                }
            }

            // Get platform-specific download URL for current platform
            var currentPlatformUrl = GetCurrentPlatformDownloadUrl(platformUrls, downloadUrl);

            return new ReleaseInfo
            {
                Version = version,
                TagName = tagName,
                ReleaseNotes = releaseNotes,
                DownloadUrl = currentPlatformUrl,
                PlatformDownloadUrls = platformUrls,
                PublishedAt = publishedAt,
                IsPrerelease = isPrerelease
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching release info: {ex.Message}");
            return null;
        }
    }

    private string GetCurrentPlatformDownloadUrl(Dictionary<string, string> platformUrls, string fallbackUrl)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Check for ARM64 first (Apple Silicon)
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64 && platformUrls.TryGetValue("osx-arm64", out var arm64Url))
            {
                return arm64Url;
            }
            // Fall back to x64
            if (platformUrls.TryGetValue("osx-x64", out var x64Url))
            {
                return x64Url;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (platformUrls.TryGetValue("win-x64", out var winUrl))
            {
                return winUrl;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (platformUrls.TryGetValue("linux-x64", out var linuxUrl))
            {
                return linuxUrl;
            }
        }

        return fallbackUrl; // Return GitHub releases page if no platform-specific URL found
    }

    private Version? ParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        // Remove 'v' prefix and any pre-release suffixes for comparison
        var cleanVersion = versionString.TrimStart('v');
        var dashIndex = cleanVersion.IndexOf('-');
        if (dashIndex > 0)
        {
            cleanVersion = cleanVersion.Substring(0, dashIndex);
        }

        if (Version.TryParse(cleanVersion, out var version))
        {
            return version;
        }

        return null;
    }

    private int CompareVersions(Version current, Version latest)
    {
        return current.CompareTo(latest);
    }

    private void LoadCachedInfo()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var json = File.ReadAllText(_cacheFilePath);
                var cache = JsonSerializer.Deserialize<UpdateCache>(json);
                if (cache != null)
                {
                    _lastCheckTime = cache.LastCheckTime;
                    if (cache.ReleaseInfo != null)
                    {
                        _latestReleaseInfo = cache.ReleaseInfo;
                        var currentVersion = ParseVersion(_versionService.InformationalVersion);
                        var latestVersion = ParseVersion(cache.ReleaseInfo.Version);
                        if (currentVersion != null && latestVersion != null)
                        {
                            _isUpdateAvailable = CompareVersions(currentVersion, latestVersion) < 0;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore cache loading errors
        }
    }

    private void SaveCachedInfo()
    {
        try
        {
            AppPaths.EnsureDirectoryExists();
            var cache = new UpdateCache
            {
                LastCheckTime = _lastCheckTime,
                ReleaseInfo = _latestReleaseInfo
            };
            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cacheFilePath, json);
        }
        catch
        {
            // Ignore cache saving errors
        }
    }

    private class UpdateCache
    {
        public DateTime? LastCheckTime { get; set; }
        public ReleaseInfo? ReleaseInfo { get; set; }
    }
}
