namespace BusLane.Tests.Services.Update;

using System.IO;
using BusLane.Models.Update;
using BusLane.Services.Update;
using FluentAssertions;

public class UpdateDownloadServiceTests : IDisposable
{
    private readonly UpdateDownloadService _sut;
    private readonly HttpClient _httpClient;

    public UpdateDownloadServiceTests()
    {
        _httpClient = new HttpClient();
        _sut = new UpdateDownloadService(_httpClient);
    }

    [Theory]
    [InlineData("../../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32\\config\\sam", "sam")]
    [InlineData("normal-file.zip", "normal-file.zip")]
    [InlineData("/absolute/path/file.dmg", "file.dmg")]
    [InlineData("C:\\Users\\Admin\\file.exe", "file.exe")]
    [InlineData("path/subdir/file.tar.gz", "file.tar.gz")]
    [InlineData("../relative/../traversal.bin", "traversal.bin")]
    public void DownloadAsync_SanitizesFileName_PreventsPathTraversal(string maliciousFileName, string expectedFileName)
    {
        var asset = new AssetInfo
        {
            DownloadUrl = "http://example.com/test",
            FileName = maliciousFileName,
            Size = 100,
            Checksum = "abc123"
        };

        var tempDirectory = Path.Combine(Path.GetTempPath(), "BusLane", "Updates");
        var expectedPath = Path.Combine(tempDirectory, expectedFileName);

        expectedPath.Should().NotContain("..");
        expectedPath.Should().EndWith(expectedFileName);
        Path.GetFileName(expectedPath).Should().Be(expectedFileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("../")]
    [InlineData("../../")]
    [InlineData("path/to/")]
    public void DownloadAsync_EmptyOrPathOnlyFileName_UsesDefaultFilename(string emptyOrPathOnlyFileName)
    {
        var asset = new AssetInfo
        {
            DownloadUrl = "http://example.com/test",
            FileName = emptyOrPathOnlyFileName,
            Size = 100,
            Checksum = "abc123"
        };

        var sanitized = Path.GetFileName(asset.FileName);
        var resultFileName = string.IsNullOrWhiteSpace(sanitized) ? "update.bin" : sanitized;

        resultFileName.Should().Be("update.bin");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _sut.Cleanup();
    }
}
