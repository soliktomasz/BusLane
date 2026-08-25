namespace BusLane.Tests.Docs;

using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

public partial class GitHubPagesLandingPageTests
{
    [Fact]
    public void LandingPage_ApprovedHero_UsesProductLedMessage()
    {
        // Arrange
        var html = ReadLandingPage();

        // Act
        var heading = HeadingRegex().Match(html);

        // Assert
        heading.Success.Should().BeTrue("the landing page should expose one primary heading");
        NormalizeText(heading.Groups[1].Value).Should().Be("Service Bus, under control.");
        html.Should().Contain("class=\"hero-media\"");
    }

    [Fact]
    public void LandingPage_DisplayPreferences_ProvideDarkAndReducedMotionModes()
    {
        // Arrange
        var html = ReadLandingPage();

        // Act
        var supportsDarkMode = html.Contains("prefers-color-scheme: dark", StringComparison.Ordinal);
        var supportsReducedMotion = html.Contains("prefers-reduced-motion: reduce", StringComparison.Ordinal);

        // Assert
        supportsDarkMode.Should().BeTrue("the page should follow the visitor's system theme");
        supportsReducedMotion.Should().BeTrue("the page should avoid forced motion");
    }

    [Fact]
    public void LandingPage_ScreenshotGallery_UsesAccessibleLocalTabs()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var html = ReadLandingPage();

        // Act
        var tabs = GalleryTabRegex().Matches(html);
        var localImages = LocalImageRegex().Matches(html).Select(match => match.Groups[1].Value).Distinct();

        // Assert
        tabs.Should().HaveCount(3);
        tabs.Count(match => match.Value.Contains("aria-selected=\"true\"", StringComparison.Ordinal)).Should().Be(1);
        localImages.Should().OnlyContain(
            path => File.Exists(Path.Combine(repositoryRoot, "docs", path)),
            "every rendered image should ship inside the GitHub Pages artifact");
    }

    [Fact]
    public void LandingPage_VisibleCopy_ContainsNoBannedDashCharacters()
    {
        // Arrange
        var html = ReadLandingPage();

        // Act
        var withoutCode = ScriptAndStyleRegex().Replace(html, string.Empty);
        var visibleCopy = HtmlTagRegex().Replace(withoutCode, " ");

        // Assert
        visibleCopy.Should().NotContain("—");
        visibleCopy.Should().NotContain("–");
    }

    [Fact]
    public void LandingPage_ProductionMarkup_RequiresNoRemoteFontOrScript()
    {
        // Arrange
        var html = ReadLandingPage();

        // Act
        var hasRemoteFont = html.Contains("fonts.googleapis.com", StringComparison.OrdinalIgnoreCase);
        var hasRemoteScript = RemoteScriptRegex().IsMatch(html);

        // Assert
        hasRemoteFont.Should().BeFalse("the page should use its system font stack without a blocking font request");
        hasRemoteScript.Should().BeFalse("the static page should not depend on third-party JavaScript");
    }

    private static string ReadLandingPage()
    {
        var path = Path.Combine(FindRepositoryRoot(), "docs", "index.html");
        File.Exists(path).Should().BeTrue($"expected GitHub Pages entry point '{path}' to exist");
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BusLane.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the test must run from within the BusLane repository");
        return directory!.FullName;
    }

    private static string NormalizeText(string value)
    {
        var withoutTags = HtmlTagRegex().Replace(value, " ");
        return WhitespaceRegex().Replace(withoutTags, " ").Trim();
    }

    [GeneratedRegex("<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("<button[^>]*class=\"gallery-tab\"[^>]*role=\"tab\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex GalleryTabRegex();

    [GeneratedRegex("(?:src|data-image)=\"(?!https?://|data:|#)([^\"]+\\.(?:png|jpe?g|webp))\"", RegexOptions.IgnoreCase)]
    private static partial Regex LocalImageRegex();

    [GeneratedRegex("<(?:script|style)[^>]*>.*?</(?:script|style)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptAndStyleRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("<script[^>]+src=\"https?://", RegexOptions.IgnoreCase)]
    private static partial Regex RemoteScriptRegex();
}
