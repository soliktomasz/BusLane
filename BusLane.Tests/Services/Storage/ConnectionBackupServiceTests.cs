namespace BusLane.Tests.Services.Storage;

using BusLane.Models;
using BusLane.Services.Storage;
using FluentAssertions;

public class ConnectionBackupServiceTests : IDisposable
{
    private readonly ConnectionBackupService _sut = new();
    private readonly string _backupPath = Path.Combine(
        Path.GetTempPath(),
        $"buslane-connections-{Guid.NewGuid():N}.blbackup");

    public void Dispose()
    {
        if (File.Exists(_backupPath))
        {
            try
            {
                File.Delete(_backupPath);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task ExportAsync_ThenImportAsync_RoundTripsConnections()
    {
        // Arrange
        const string passphrase = "strong-passphrase-123!";
        var connections = new List<SavedConnection>
        {
            CreateConnection("prod-1", "Production"),
            CreateConnection("test-2", "Test")
        };

        // Act
        await _sut.ExportAsync(connections, _backupPath, passphrase);
        var imported = await _sut.ImportAsync(_backupPath, passphrase);

        // Assert
        imported.Should().HaveCount(2);
        imported.Should().Contain(c => c.Id == "prod-1" && c.Name == "Production");
        imported.Should().Contain(c => c.Id == "test-2" && c.Name == "Test");
        imported.Should().Contain(c => c.ConnectionString.Contains("SharedAccessKey=abc123"));
    }

    [Fact]
    public async Task ExportAsync_BackupFile_DoesNotContainPlainConnectionString()
    {
        // Arrange
        const string passphrase = "backup-secret";
        const string rawConnectionString =
            "Endpoint=sb://mybus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123";
        var connections = new List<SavedConnection>
        {
            CreateConnection("id-1", "Prod", rawConnectionString)
        };

        // Act
        await _sut.ExportAsync(connections, _backupPath, passphrase);
        var backupContent = await File.ReadAllTextAsync(_backupPath);

        // Assert
        backupContent.Should().NotContain(rawConnectionString);
        backupContent.Should().NotContain("SharedAccessKey=abc123");
    }

    [Fact]
    public async Task ImportAsync_WithWrongPassphrase_ThrowsCryptographicException()
    {
        // Arrange
        var connections = new List<SavedConnection>
        {
            CreateConnection("id-1", "Prod")
        };

        await _sut.ExportAsync(connections, _backupPath, "correct-passphrase");

        // Act
        var act = async () => await _sut.ImportAsync(_backupPath, "wrong-passphrase");

        // Assert
        await act.Should().ThrowAsync<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public async Task ImportAsync_WithExcessiveIterations_ThrowsInvalidDataException()
    {
        // Arrange
        await _sut.ExportAsync(
            [CreateConnection("id-1", "Prod")],
            _backupPath,
            "correct-passphrase");

        var backupJson = await File.ReadAllTextAsync(_backupPath);
        backupJson = backupJson.Replace("\"iterations\": 210000", "\"iterations\": 2147483647");
        await File.WriteAllTextAsync(_backupPath, backupJson);

        // Act
        var act = async () => await _sut.ImportAsync(_backupPath, "correct-passphrase");

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    private static SavedConnection CreateConnection(
        string id,
        string name,
        string? connectionString = null)
    {
        return new SavedConnection
        {
            Id = id,
            Name = name,
            ConnectionString = connectionString ??
                "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            Type = ConnectionType.Namespace,
            CreatedAt = DateTimeOffset.UtcNow,
            Environment = ConnectionEnvironment.None
        };
    }
}
