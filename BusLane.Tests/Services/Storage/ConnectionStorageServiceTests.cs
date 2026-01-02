using BusLane.Models;
using BusLane.Services.Infrastructure;
using BusLane.Services.Storage;
using FluentAssertions;
using NSubstitute;

namespace BusLane.Tests.Services.Storage;

public class ConnectionStorageServiceTests : IDisposable
{
    private readonly IEncryptionService _encryptionService;
    private readonly ConnectionStorageService _sut;
    private readonly string _testStoragePath;

    public ConnectionStorageServiceTests()
    {
        _encryptionService = Substitute.For<IEncryptionService>();
        
        // Setup default encryption behavior - just add prefix for testing
        _encryptionService.Encrypt(Arg.Any<string>())
            .Returns(x => $"ENC:{x.Arg<string>()}");
        _encryptionService.Decrypt(Arg.Any<string>())
            .Returns(x =>
            {
                var input = x.Arg<string>();
                return input.StartsWith("ENC:") ? input[4..] : input;
            });

        _sut = new ConnectionStorageService(_encryptionService);
        
        // Get the storage path for cleanup
        _testStoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BusLane",
            "connections.json"
        );
    }

    public void Dispose()
    {
        // Clean up test file after tests
        if (File.Exists(_testStoragePath))
        {
            try { File.Delete(_testStoragePath); } catch { }
        }
    }

    [Fact]
    public async Task SaveConnectionAsync_EncryptsConnectionString()
    {
        // Arrange
        var connection = CreateTestConnection();

        // Act
        await _sut.SaveConnectionAsync(connection);

        // Assert
        _encryptionService.Received(1).Encrypt(connection.ConnectionString);
    }

    [Fact]
    public async Task GetConnectionsAsync_DecryptsConnectionStrings()
    {
        // Arrange
        var connection = CreateTestConnection();
        await _sut.SaveConnectionAsync(connection);

        // Act
        var connections = await _sut.GetConnectionsAsync();

        // Assert
        _encryptionService.Received().Decrypt(Arg.Any<string>());
        connections.Should().Contain(c => c.ConnectionString == connection.ConnectionString);
    }

    [Fact]
    public async Task SaveConnectionAsync_WithSameId_UpdatesExistingConnection()
    {
        // Arrange
        var connection1 = CreateTestConnection("conn1", "Original Name");
        var connection2 = CreateTestConnection("conn1", "Updated Name");

        // Act
        await _sut.SaveConnectionAsync(connection1);
        await _sut.SaveConnectionAsync(connection2);
        var connections = await _sut.GetConnectionsAsync();

        // Assert
        connections.Should().ContainSingle()
            .Which.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteConnectionAsync_RemovesConnection()
    {
        // Arrange
        var connection = CreateTestConnection();
        await _sut.SaveConnectionAsync(connection);

        // Act
        await _sut.DeleteConnectionAsync(connection.Id);
        var connections = await _sut.GetConnectionsAsync();

        // Assert
        connections.Should().NotContain(c => c.Id == connection.Id);
    }

    [Fact]
    public async Task GetConnectionAsync_WithExistingId_ReturnsConnection()
    {
        // Arrange
        var connection = CreateTestConnection();
        await _sut.SaveConnectionAsync(connection);

        // Act
        var result = await _sut.GetConnectionAsync(connection.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(connection.Id);
    }

    [Fact]
    public async Task GetConnectionAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _sut.GetConnectionAsync("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearAllConnectionsAsync_RemovesAllConnections()
    {
        // Arrange
        await _sut.SaveConnectionAsync(CreateTestConnection("conn1"));
        await _sut.SaveConnectionAsync(CreateTestConnection("conn2"));
        await _sut.SaveConnectionAsync(CreateTestConnection("conn3"));

        // Act
        await _sut.ClearAllConnectionsAsync();
        var connections = await _sut.GetConnectionsAsync();

        // Assert
        connections.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConnectionsAsync_WithNoConnections_ReturnsEmptyCollection()
    {
        // Arrange
        await _sut.ClearAllConnectionsAsync();

        // Act
        var connections = await _sut.GetConnectionsAsync();

        // Assert
        connections.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveConnectionAsync_PreservesAllProperties()
    {
        // Arrange
        var connection = new SavedConnection
        {
            Id = "test-id",
            Name = "Test Connection",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/",
            Type = ConnectionType.Queue,
            EntityName = "test-queue",
            CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0),
            IsFavorite = true,
            Environment = ConnectionEnvironment.Production
        };

        // Act
        await _sut.SaveConnectionAsync(connection);
        var result = await _sut.GetConnectionAsync(connection.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(connection.Name);
        result.Type.Should().Be(ConnectionType.Queue);
        result.EntityName.Should().Be("test-queue");
        result.IsFavorite.Should().BeTrue();
        result.Environment.Should().Be(ConnectionEnvironment.Production);
    }

    [Fact]
    public async Task GetConnectionsAsync_WithLegacyUnencryptedData_HandlesBackwardCompatibility()
    {
        // Arrange - Decryption returns unmodified string for unencrypted data
        _encryptionService.Decrypt(Arg.Any<string>())
            .Returns(x => x.Arg<string>()); // Returns input as-is (backward compat)

        var connection = CreateTestConnection();
        await _sut.SaveConnectionAsync(connection);

        // Act
        var connections = await _sut.GetConnectionsAsync();

        // Assert
        connections.Should().NotBeEmpty();
    }

    private static SavedConnection CreateTestConnection(
        string? id = null,
        string name = "Test Connection")
    {
        return new SavedConnection
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Name = name,
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Test;SharedAccessKey=abc123",
            Type = ConnectionType.Namespace,
            CreatedAt = DateTime.UtcNow,
            IsFavorite = false,
            Environment = ConnectionEnvironment.None
        };
    }
}

