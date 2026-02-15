namespace BusLane.Tests.Services.Infrastructure;

using BusLane.Services.Infrastructure;
using FluentAssertions;

public class AppPathsTests : IDisposable
{
    private readonly string _testDir;

    public AppPathsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"BusLaneTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CreateSecureFile_CreatesFileWithContent()
    {
        // Arrange
        var testFile = Path.Combine(_testDir, "secure_test.json");
        var content = "{ \"test\": \"data\" }";

        // Act
        AppPaths.CreateSecureFile(testFile, content);

        // Assert
        File.Exists(testFile).Should().BeTrue();
        File.ReadAllText(testFile).Should().Be(content);
    }

    [Fact]
    public void CreateSecureFile_CreatesParentDirectoryIfNotExists()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "subdir1", "subdir2");
        var testFile = Path.Combine(subDir, "secure_test.json");
        var content = "test content";

        // Act
        AppPaths.CreateSecureFile(testFile, content);

        // Assert
        Directory.Exists(subDir).Should().BeTrue();
        File.Exists(testFile).Should().BeTrue();
    }

    [Fact]
    public void CreateSecureFile_OverwritesExistingFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDir, "overwrite_test.json");
        File.WriteAllText(testFile, "old content");
        var newContent = "new secure content";

        // Act
        AppPaths.CreateSecureFile(testFile, newContent);

        // Assert
        File.ReadAllText(testFile).Should().Be(newContent);
    }

    [Fact]
    public void CreateSecureFile_OnUnix_Sets0600Permissions()
    {
        // Only run on Unix-like systems (macOS/Linux)
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        // Arrange
        var testFile = Path.Combine(_testDir, "permissions_test.json");
        var content = "sensitive data";

        // Act
        AppPaths.CreateSecureFile(testFile, content);

        // Assert
        var fileMode = File.GetUnixFileMode(testFile);
        fileMode.Should().HaveFlag(UnixFileMode.UserRead);
        fileMode.Should().HaveFlag(UnixFileMode.UserWrite);
        fileMode.Should().NotHaveFlag(UnixFileMode.GroupRead);
        fileMode.Should().NotHaveFlag(UnixFileMode.GroupWrite);
        fileMode.Should().NotHaveFlag(UnixFileMode.GroupExecute);
        fileMode.Should().NotHaveFlag(UnixFileMode.OtherRead);
        fileMode.Should().NotHaveFlag(UnixFileMode.OtherWrite);
        fileMode.Should().NotHaveFlag(UnixFileMode.OtherExecute);
    }

    [Theory]
    [InlineData("")]
    [InlineData("single line")]
    [InlineData("multi\nline\ncontent")]
    [InlineData("special chars: !@#$%^&*()")]
    [InlineData("unicode: привет 世界")]
    public void CreateSecureFile_HandlesVariousContentTypes(string content)
    {
        // Arrange
        var testFile = Path.Combine(_testDir, $"content_test_{Guid.NewGuid():N}.txt");

        // Act
        AppPaths.CreateSecureFile(testFile, content);

        // Assert
        File.ReadAllText(testFile).Should().Be(content);
    }
}
