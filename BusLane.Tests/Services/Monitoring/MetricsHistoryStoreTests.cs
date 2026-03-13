namespace BusLane.Tests.Services.Monitoring;

using BusLane.Models;
using BusLane.Services.Monitoring;
using FluentAssertions;

public class MetricsHistoryStoreTests : IDisposable
{
    private readonly string _storePath = Path.Combine(Path.GetTempPath(), $"metrics-history-{Guid.NewGuid():N}.json");

    [Fact]
    public void SaveSnapshotsAndReload_PreservesSnapshots()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 3, 7, 10, 0, 0, TimeSpan.Zero);
        var sut = new MetricsHistoryStore(_storePath, TimeSpan.FromDays(7), () => now);

        // Act
        sut.RecordSnapshots(
        [
            new MetricSnapshot(now.AddMinutes(-10), "orders", "ActiveMessageCount", 12),
            new MetricSnapshot(now.AddMinutes(-5), "orders", "DeadLetterCount", 2)
        ]);

        var reloaded = new MetricsHistoryStore(_storePath, TimeSpan.FromDays(7), () => now);

        // Assert
        reloaded.GetHistory("orders", "ActiveMessageCount", TimeSpan.FromHours(1))
            .Should()
            .ContainSingle(s => s.Value == 12);
        reloaded.GetHistory("orders", "DeadLetterCount", TimeSpan.FromHours(1))
            .Should()
            .ContainSingle(s => s.Value == 2);
    }

    [Fact]
    public void CleanupExpiredSnapshots_RemovesOlderThanRetention()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 3, 7, 10, 0, 0, TimeSpan.Zero);
        var sut = new MetricsHistoryStore(_storePath, TimeSpan.FromHours(1), () => now);
        sut.RecordSnapshots(
        [
            new MetricSnapshot(now.AddHours(-2), "orders", "ActiveMessageCount", 10),
            new MetricSnapshot(now.AddMinutes(-20), "orders", "ActiveMessageCount", 20)
        ]);

        // Act
        sut.CleanupExpiredSnapshots();

        // Assert
        sut.GetHistory("orders", "ActiveMessageCount", TimeSpan.FromHours(3))
            .Should()
            .ContainSingle(s => s.Value == 20);
    }

    [Fact]
    public void CompareWindows_ComputesCurrentAndPreviousAverages()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 3, 7, 10, 0, 0, TimeSpan.Zero);
        var sut = new MetricsHistoryStore(_storePath, TimeSpan.FromDays(7), () => now);
        sut.RecordSnapshots(
        [
            new MetricSnapshot(now.AddMinutes(-50), "orders", "ActiveMessageCount", 10),
            new MetricSnapshot(now.AddMinutes(-40), "orders", "ActiveMessageCount", 20),
            new MetricSnapshot(now.AddMinutes(-20), "orders", "ActiveMessageCount", 30),
            new MetricSnapshot(now.AddMinutes(-10), "orders", "ActiveMessageCount", 50)
        ]);

        // Act
        var comparison = sut.CompareWindows("orders", "ActiveMessageCount", TimeSpan.FromMinutes(30));

        // Assert
        comparison.CurrentAverage.Should().Be(40);
        comparison.PreviousAverage.Should().Be(15);
        comparison.Delta.Should().Be(25);
    }

    [Fact]
    public void CompareWindows_AfterInitialLoad_UsesCachedSnapshots()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 3, 7, 10, 0, 0, TimeSpan.Zero);
        var sut = new MetricsHistoryStore(_storePath, TimeSpan.FromDays(7), () => now);
        sut.RecordSnapshots(
        [
            new MetricSnapshot(now.AddMinutes(-50), "orders", "ActiveMessageCount", 10),
            new MetricSnapshot(now.AddMinutes(-40), "orders", "ActiveMessageCount", 20),
            new MetricSnapshot(now.AddMinutes(-20), "orders", "ActiveMessageCount", 30),
            new MetricSnapshot(now.AddMinutes(-10), "orders", "ActiveMessageCount", 50)
        ]);

        sut.CompareWindows("orders", "ActiveMessageCount", TimeSpan.FromMinutes(30)).Delta.Should().Be(25);
        File.Delete(_storePath);

        // Act
        var comparison = sut.CompareWindows("orders", "ActiveMessageCount", TimeSpan.FromMinutes(30));

        // Assert
        comparison.CurrentAverage.Should().Be(40);
        comparison.PreviousAverage.Should().Be(15);
        comparison.Delta.Should().Be(25);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }
}
