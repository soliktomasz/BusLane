namespace BusLane.Tests.ViewModels.Core;

using BusLane.Services.Abstractions;
using BusLane.Services.Terminal;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;

public class TerminalHostViewModelTests
{
    [Fact]
    public void ToggleVisibility_ShouldPersistPreference()
    {
        // Arrange
        var preferences = CreatePreferences();
        var terminalService = new FakeTerminalSessionService();
        using var sut = new TerminalHostViewModel(terminalService, preferences);

        // Act
        sut.ToggleVisibilityCommand.Execute(null);

        // Assert
        sut.ShowTerminalPanel.Should().BeTrue();
        preferences.ShowTerminalPanel.Should().BeTrue();
        preferences.Received().Save();
    }

    [Fact]
    public async Task UndockAndDock_ShouldKeepSameSessionAndOutput()
    {
        // Arrange
        var preferences = CreatePreferences();
        var terminalService = new FakeTerminalSessionService();
        using var sut = new TerminalHostViewModel(terminalService, preferences);

        sut.ToggleVisibilityCommand.Execute(null);
        await Task.Delay(10);
        var initialSessionId = sut.SessionId;

        // Act
        terminalService.EmitOutput("hello from terminal\n");
        await Task.Delay(10);
        sut.UndockCommand.Execute(null);
        sut.DockCommand.Execute(null);

        // Assert
        sut.SessionId.Should().Be(initialSessionId);
        sut.OutputText.Should().Contain("hello from terminal");
    }

    [Fact]
    public async Task Restart_ShouldCreateNewSession()
    {
        // Arrange
        var preferences = CreatePreferences();
        var terminalService = new FakeTerminalSessionService();
        using var sut = new TerminalHostViewModel(terminalService, preferences);

        sut.ToggleVisibilityCommand.Execute(null);
        await Task.Delay(10);
        var initialSessionId = sut.SessionId;

        // Act
        await sut.RestartCommand.ExecuteAsync(null);

        // Assert
        sut.SessionId.Should().NotBe(initialSessionId);
        sut.IsRunning.Should().BeTrue();
    }

    private static IPreferencesService CreatePreferences()
    {
        var preferences = Substitute.For<IPreferencesService>();
        preferences.ShowTerminalPanel = false;
        preferences.TerminalIsDocked = true;
        preferences.TerminalDockHeight = 260;
        preferences.TerminalWindowBoundsJson = null;
        return preferences;
    }

    private sealed class FakeTerminalSessionService : ITerminalSessionService
    {
        public event EventHandler<string>? OutputReceived;
        public event EventHandler<int?>? SessionExited;

        public Guid SessionId { get; private set; } = Guid.Empty;
        public bool IsRunning { get; private set; }

        public Task StartAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            IsRunning = true;
            SessionId = Guid.NewGuid();
            return Task.CompletedTask;
        }

        public Task WriteAsync(string text, CancellationToken ct = default)
        {
            _ = text;
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task ResizeAsync(int cols, int rows, CancellationToken ct = default)
        {
            _ = cols;
            _ = rows;
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public async Task RestartAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            IsRunning = false;
            await StartAsync(ct);
        }

        public ValueTask DisposeAsync()
        {
            IsRunning = false;
            return ValueTask.CompletedTask;
        }

        public void EmitOutput(string text)
        {
            OutputReceived?.Invoke(this, text);
        }

        public void EmitExit(int? code)
        {
            IsRunning = false;
            SessionExited?.Invoke(this, code);
        }
    }
}
