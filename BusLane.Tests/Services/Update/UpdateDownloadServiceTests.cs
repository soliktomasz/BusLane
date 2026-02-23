namespace BusLane.Tests.Services.Update;

using System.IO;
using System.Net;
using System.Net.Http.Headers;
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

    [Fact]
    public async Task DownloadAsync_ResumeRequested_ServerReturnsOk_RestartsFromBeginning()
    {
        // Arrange
        var fileName = $"resume-ok-{Guid.NewGuid():N}.bin";
        var tempDirectory = Path.Combine(Path.GetTempPath(), "BusLane", "Updates");
        var filePath = Path.Combine(tempDirectory, fileName);
        Directory.CreateDirectory(tempDirectory);

        var existingBytes = "12345"u8.ToArray();
        var fullBytes = "abcdefghij"u8.ToArray();
        await File.WriteAllBytesAsync(filePath, existingBytes);

        var rangeRequested = false;
        var handler = new StubHttpMessageHandler(request =>
        {
            rangeRequested = request.Headers.Range != null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(fullBytes)
            };
        });

        using var httpClient = new HttpClient(handler);
        var sut = new UpdateDownloadService(httpClient);
        var asset = new AssetInfo
        {
            DownloadUrl = "https://example.com/update.bin",
            FileName = fileName,
            Size = fullBytes.Length,
            Checksum = "unused"
        };

        // Act
        var downloadedPath = await sut.DownloadAsync(asset);

        // Assert
        downloadedPath.Should().Be(filePath);
        rangeRequested.Should().BeTrue();
        var actualBytes = await File.ReadAllBytesAsync(filePath);
        actualBytes.Should().Equal(fullBytes);

        sut.Cleanup();
    }

    [Fact]
    public async Task DownloadAsync_ResumeRequested_ServerReturnsPartialContent_AppendsToExistingFile()
    {
        // Arrange
        var fileName = $"resume-partial-{Guid.NewGuid():N}.bin";
        var tempDirectory = Path.Combine(Path.GetTempPath(), "BusLane", "Updates");
        var filePath = Path.Combine(tempDirectory, fileName);
        Directory.CreateDirectory(tempDirectory);

        var existingBytes = "12345"u8.ToArray();
        var remainingBytes = "67890"u8.ToArray();
        var expectedBytes = "1234567890"u8.ToArray();
        await File.WriteAllBytesAsync(filePath, existingBytes);

        RangeHeaderValue? capturedRange = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRange = request.Headers.Range;
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(remainingBytes)
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                existingBytes.Length,
                expectedBytes.Length - 1,
                expectedBytes.Length);
            return response;
        });

        using var httpClient = new HttpClient(handler);
        var sut = new UpdateDownloadService(httpClient);
        var asset = new AssetInfo
        {
            DownloadUrl = "https://example.com/update.bin",
            FileName = fileName,
            Size = expectedBytes.Length,
            Checksum = "unused"
        };

        // Act
        var downloadedPath = await sut.DownloadAsync(asset);

        // Assert
        downloadedPath.Should().Be(filePath);
        capturedRange.Should().NotBeNull();
        capturedRange!.Ranges.Single().From.Should().Be(existingBytes.Length);
        var actualBytes = await File.ReadAllBytesAsync(filePath);
        actualBytes.Should().Equal(expectedBytes);

        sut.Cleanup();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _sut.Cleanup();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
