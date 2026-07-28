namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;

public class ReplayAuditStoreTests
{
    [Fact]
    public async Task AddAsync_RoundTripsAuditEntriesWithoutSensitiveConnectionData()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"buslane-replay-audit-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "replay-audit.json");
        var sut = new ReplayAuditStore(path);
        var entry = CreateEntry(ReplayAuditOutcome.Succeeded);

        try
        {
            // Act
            await sut.AddAsync(entry);
            var loaded = await sut.LoadAsync();
            var json = await File.ReadAllTextAsync(path);

            // Assert
            loaded.Should().ContainSingle().Which.Should().BeEquivalentTo(entry);
            json.Should().NotContain("ConnectionString");
            json.Should().NotContain("SharedAccessKey");
            json.Should().NotContain("Bearer ");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddAsync_CreatesOwnerOnlyFileOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"buslane-replay-audit-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "replay-audit.json");
        var sut = new ReplayAuditStore(path);

        try
        {
            // Act
            await sut.AddAsync(CreateEntry(ReplayAuditOutcome.Attempted));

            // Assert
            File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static ReplayAuditEntry CreateEntry(ReplayAuditOutcome outcome)
    {
        return new ReplayAuditEntry(
            Guid.NewGuid().ToString(),
            DateTimeOffset.Parse("2026-07-28T10:00:00Z"),
            outcome,
            "message-1",
            "corr-1",
            "demo.servicebus.windows.net",
            ConnectionEnvironment.Test,
            "orders-replay",
            IsScheduled: false,
            RateLimitPerSecond: 2,
            ChangedFields: ["MessageId"],
            ValidationMessages: [],
            ResultMessage: "Message replayed successfully");
    }
}
