namespace BusLane.Tests;

using System.IO;
using System.Security.Cryptography;
using FluentAssertions;
using SkiaSharp;

public class AppIconAssetTests
{
    private const int MinimumTransparentInsetPixels = 20;

    [Fact]
    public void MacOsSourceIcon_Always_MatchesBundled512PixelDockAssets()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var sourceIconPath = Path.Combine(repositoryRoot, "BusLane", "Assets", "icon.png");
        var dockAssetPaths = new[]
        {
            Path.Combine(repositoryRoot, "BusLane", "Assets", "icon.iconset", "icon_512x512.png"),
            Path.Combine(repositoryRoot, "BusLane", "Assets", "icon.iconset", "icon_256x256@2x.png")
        };

        // Act
        var sourceHash = ComputeSha256(sourceIconPath);
        var dockAssetHashes = dockAssetPaths.Select(ComputeSha256).ToArray();

        // Assert
        dockAssetHashes.Should().OnlyContain(
            hash => hash == sourceHash,
            "the dock icon should be generated from the current source icon so macOS uses the same artwork as the rest of the app");
    }

    [Fact]
    public void SourceIcon_Always_KeepsTransparentInsetForMacOsDockSizing()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var sourceIconPath = Path.Combine(repositoryRoot, "BusLane", "Assets", "icon.png");

        // Act
        var bounds = GetVisibleBounds(sourceIconPath);

        // Assert
        bounds.Left.Should().BeGreaterThanOrEqualTo(
            MinimumTransparentInsetPixels,
            "a full-bleed icon renders optically larger than neighboring macOS dock icons");
        bounds.Top.Should().BeGreaterThanOrEqualTo(MinimumTransparentInsetPixels);
        bounds.RightInset.Should().BeGreaterThanOrEqualTo(MinimumTransparentInsetPixels);
        bounds.BottomInset.Should().BeGreaterThanOrEqualTo(MinimumTransparentInsetPixels);
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

    private static string ComputeSha256(string path)
    {
        File.Exists(path).Should().BeTrue($"expected icon asset '{path}' to exist");
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static IconBounds GetVisibleBounds(string path)
    {
        File.Exists(path).Should().BeTrue($"expected icon asset '{path}' to exist");

        using var bitmap = SKBitmap.Decode(path);
        bitmap.Should().NotBeNull("the icon should be readable as a bitmap");
        var width = bitmap!.Width;
        var height = bitmap.Height;

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        maxX.Should().BeGreaterThanOrEqualTo(0, "the icon should contain visible pixels");
        maxY.Should().BeGreaterThanOrEqualTo(0, "the icon should contain visible pixels");

        return new IconBounds(
            minX,
            minY,
            (width - 1) - maxX,
            (height - 1) - maxY);
    }

    private sealed record IconBounds(int Left, int Top, int RightInset, int BottomInset);
}
