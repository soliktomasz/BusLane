namespace BusLane.Tests.Services.Update;

using BusLane.Services.Update;
using FluentAssertions;

public class UpdateCheckServiceTests
{
    [Fact]
    public void ParseGitHubRelease_WithValidJson_ReturnsReleaseInfo()
    {
        // Arrange
        var json = """
        {
            "tag_name": "v0.10.0",
            "published_at": "2026-01-30T12:00:00Z",
            "prerelease": false,
            "body": "Release notes",
            "assets": [
                {
                    "name": "BusLane-0.10.0-win-x64.msi",
                    "browser_download_url": "https://example.com/download.msi",
                    "size": 1000000
                }
            ]
        }
        """;

        // Act
        var result = UpdateCheckService.ParseGitHubRelease(json, "win-x64");

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("0.10.0");
        result.IsPrerelease.Should().BeFalse();
        result.Assets.Should().ContainKey("win-x64");
    }

    [Theory]
    [InlineData("0.9.4", "0.10.0", true)]
    [InlineData("0.10.0", "0.9.4", false)]
    [InlineData("0.10.0", "0.10.0", false)]
    [InlineData("1.0.0", "0.10.0", false)]
    public void IsNewerVersion_ComparesCorrectly(string current, string latest, bool expected)
    {
        // Act
        var result = UpdateCheckService.IsNewerVersion(current, latest);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("BusLane-0.10.0-win-x64.msi", "win-x64", true)]
    [InlineData("BusLane-0.10.0-osx-arm64.dmg", "osx-arm64", true)]
    [InlineData("BusLane-0.10.0-linux-x64.AppImage", "linux-x64", true)]
    [InlineData("BusLane-0.10.0-win-x64.msi", "osx-arm64", false)]
    public void MatchesPlatform_ChecksCorrectly(string filename, string platform, bool expected)
    {
        // Act
        var result = UpdateCheckService.MatchesPlatform(filename, platform);

        // Assert
        result.Should().Be(expected);
    }
}
