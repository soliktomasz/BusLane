namespace BusLane.Tests.ViewModels;

using System.Collections.Specialized;
using BusLane.Models.Logging;
using BusLane.Services.Infrastructure;
using BusLane.ViewModels;
using FluentAssertions;

public class LogViewerViewModelTests
{
    [Fact]
    public void LogErrorWhileClosed_SetsUnreadErrorIndicator()
    {
        // Arrange
        var logSink = new LogSink();
        using var sut = new LogViewerViewModel(logSink);

        // Act
        logSink.Log(CreateEntry(LogLevel.Error));

        // Assert
        sut.HasUnreadErrors.Should().BeTrue();
    }

    [Fact]
    public void LogInfoWhileClosed_DoesNotSetUnreadErrorIndicator()
    {
        // Arrange
        var logSink = new LogSink();
        using var sut = new LogViewerViewModel(logSink);

        // Act
        logSink.Log(CreateEntry(LogLevel.Info));

        // Assert
        sut.HasUnreadErrors.Should().BeFalse();
    }

    [Fact]
    public void Open_WhenUnreadErrorExists_ClearsUnreadErrorIndicator()
    {
        // Arrange
        var logSink = new LogSink();
        using var sut = new LogViewerViewModel(logSink);
        logSink.Log(CreateEntry(LogLevel.Error));

        // Act
        sut.Open();

        // Assert
        sut.HasUnreadErrors.Should().BeFalse();
    }

    [Fact]
    public void ClearLogs_WhenUnreadErrorExists_ClearsUnreadErrorIndicator()
    {
        // Arrange
        var logSink = new LogSink();
        using var sut = new LogViewerViewModel(logSink);
        logSink.Log(CreateEntry(LogLevel.Error));

        // Act
        sut.ClearLogsCommand.Execute(null);

        // Assert
        sut.HasUnreadErrors.Should().BeFalse();
    }

    [Fact]
    public void LogAdded_WhenEntryMatchesCurrentFilters_AppendsWithoutResettingCollection()
    {
        // Arrange
        var logSink = new LogSink();
        using var sut = new LogViewerViewModel(logSink);
        sut.SelectedLevelFilter = LogLevel.Info;

        var actions = new List<NotifyCollectionChangedAction>();
        sut.FilteredLogs.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        logSink.Log(CreateEntry(LogLevel.Info));

        // Assert
        sut.FilteredLogs.Should().ContainSingle();
        actions.Should().Contain(NotifyCollectionChangedAction.Add);
        actions.Should().NotContain(NotifyCollectionChangedAction.Reset);
    }

    [Fact]
    public void LogAdded_WhenDuplicateValuesOverflow_RemovesTrimmedInstanceNotNewestMatch()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var logSink = new LogSink();
        using var sut = new LogViewerViewModel(logSink);

        for (var i = 0; i < 1000; i++)
        {
            logSink.Log(CreateEntry(LogLevel.Info, timestamp));
        }

        var newestEntry = CreateEntry(LogLevel.Info, timestamp);

        // Act
        logSink.Log(newestEntry);

        // Assert
        sut.FilteredLogs.Should().HaveCount(1000);
        sut.FilteredLogs[0].Should().BeSameAs(newestEntry);
    }

    private static LogEntry CreateEntry(LogLevel level, DateTime? timestamp = null)
    {
        return new LogEntry(
            timestamp ?? DateTime.UtcNow,
            LogSource.Application,
            level,
            $"Test {level}");
    }
}
