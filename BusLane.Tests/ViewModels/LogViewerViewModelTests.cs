namespace BusLane.Tests.ViewModels;

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

    private static LogEntry CreateEntry(LogLevel level)
    {
        return new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            level,
            $"Test {level}");
    }
}
