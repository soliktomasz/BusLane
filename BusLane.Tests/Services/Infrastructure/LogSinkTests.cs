namespace BusLane.Tests.Services.Infrastructure;

using BusLane.Models.Logging;
using BusLane.Services.Infrastructure;
using FluentAssertions;

public class LogSinkTests
{
    private readonly LogSink _sut;

    public LogSinkTests()
    {
        _sut = new LogSink(maxEntries: 10);
    }

    [Fact]
    public void Log_AddsEntryToCollection()
    {
        // Arrange
        var entry = CreateTestEntry();

        // Act
        _sut.Log(entry);

        // Assert
        var logs = _sut.GetLogs();
        logs.Should().Contain(entry);
        logs.Count.Should().Be(1);
    }

    [Fact]
    public void Log_TriggersOnLogAddedEvent()
    {
        // Arrange
        var entry = CreateTestEntry();
        LogEntry? capturedEntry = null;
        _sut.OnLogAdded += e => capturedEntry = e;

        // Act
        _sut.Log(entry);

        // Assert
        capturedEntry.Should().Be(entry);
    }

    [Fact]
    public void GetLogs_ReturnsAllLoggedEntries()
    {
        // Arrange
        var entries = new[]
        {
            CreateTestEntry(LogLevel.Info),
            CreateTestEntry(LogLevel.Warning),
            CreateTestEntry(LogLevel.Error)
        };

        foreach (var entry in entries)
        {
            _sut.Log(entry);
        }

        // Act
        var logs = _sut.GetLogs();

        // Assert
        logs.Count.Should().Be(3);
        logs.Should().Contain(entries[0]);
        logs.Should().Contain(entries[1]);
        logs.Should().Contain(entries[2]);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        _sut.Log(CreateTestEntry());
        _sut.Log(CreateTestEntry());

        // Act
        _sut.Clear();

        // Assert
        _sut.GetLogs().Count.Should().Be(0);
    }

    [Fact]
    public void Log_RespectsMaxEntriesLimit()
    {
        // Arrange - Log more entries than the max (10)
        for (int i = 0; i < 15; i++)
        {
            _sut.Log(CreateTestEntry(message: $"Entry {i}"));
        }

        // Act
        var logs = _sut.GetLogs();

        // Assert - Should only have 10 entries (oldest removed)
        logs.Count.Should().Be(10);
        logs.Should().Contain(e => e.Message == "Entry 5");
        logs.Should().NotContain(e => e.Message == "Entry 0");
    }

    [Fact]
    public void Log_MaintainsOrderByTimestampDescending()
    {
        // Arrange
        var entry1 = new LogEntry(DateTime.UtcNow.AddSeconds(-2), LogSource.Application, LogLevel.Info, "First");
        var entry2 = new LogEntry(DateTime.UtcNow.AddSeconds(-1), LogSource.Application, LogLevel.Info, "Second");
        var entry3 = new LogEntry(DateTime.UtcNow, LogSource.Application, LogLevel.Info, "Third");

        _sut.Log(entry1);
        _sut.Log(entry2);
        _sut.Log(entry3);

        // Act
        var logs = _sut.GetLogs();

        // Assert
        logs[0].Message.Should().Be("Third");
        logs[1].Message.Should().Be("Second");
        logs[2].Message.Should().Be("First");
    }

    [Fact]
    public void Log_CircularBuffer_RemovesOldestWhenFull()
    {
        // Arrange - Fill up to capacity
        for (int i = 0; i < 10; i++)
        {
            _sut.Log(CreateTestEntry(message: $"Entry {i}"));
        }

        // Act - Add one more (should remove entry 0)
        _sut.Log(CreateTestEntry(message: "New Entry"));

        // Assert
        var logs = _sut.GetLogs();
        logs.Count.Should().Be(10);
        logs.Should().Contain(e => e.Message == "New Entry");
        logs.Should().NotContain(e => e.Message == "Entry 0");
        logs.Should().Contain(e => e.Message == "Entry 1");
    }

    [Fact]
    public void Log_EntryWithDetails_StoresDetails()
    {
        // Arrange
        var entry = new LogEntry(
            DateTime.UtcNow,
            LogSource.ServiceBus,
            LogLevel.Info,
            "Test message",
            "Detailed information here");

        // Act
        _sut.Log(entry);

        // Assert
        var logs = _sut.GetLogs();
        logs.Should().Contain(e => e.Details == "Detailed information here");
    }

    [Fact]
    public void Log_EntryWithNullDetails_StoresNull()
    {
        // Arrange
        var entry = new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            "Test message",
            null);

        // Act
        _sut.Log(entry);

        // Assert
        var logs = _sut.GetLogs();
        logs.Should().Contain(e => e.Details == null);
    }

    private static LogEntry CreateTestEntry(
        LogLevel level = LogLevel.Info,
        LogSource source = LogSource.Application,
        string message = "Test message")
    {
        return new LogEntry(
            DateTime.UtcNow,
            source,
            level,
            message);
    }
}
