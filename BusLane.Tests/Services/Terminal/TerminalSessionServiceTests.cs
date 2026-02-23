namespace BusLane.Tests.Services.Terminal;

using BusLane.Services.Terminal;
using FluentAssertions;

public class TerminalSessionServiceTests
{
    [Fact]
    public async Task StartAsync_WithEchoCommand_EmitsOutput()
    {
        // Arrange
        await using var sut = new TerminalSessionService();
        var token = $"buslane-terminal-{Guid.NewGuid():N}";
        var outputTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        sut.OutputReceived += (_, output) =>
        {
            if (output.Contains(token, StringComparison.Ordinal))
            {
                outputTcs.TrySetResult(output);
            }
        };

        // Act
        await sut.StartAsync();
        await sut.WriteAsync($"echo {token}{Environment.NewLine}");

        // Assert
        sut.IsRunning.Should().BeTrue();
        var completed = await Task.WhenAny(outputTcs.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        completed.Should().Be(outputTcs.Task);
    }

    [Fact]
    public async Task RestartAsync_CreatesNewSessionId()
    {
        // Arrange
        await using var sut = new TerminalSessionService();
        await sut.StartAsync();
        var initialSessionId = sut.SessionId;

        // Act
        await sut.RestartAsync();

        // Assert
        sut.IsRunning.Should().BeTrue();
        sut.SessionId.Should().NotBe(initialSessionId);
    }

    [Fact]
    public async Task ExitCommand_ShouldRaiseSessionExited()
    {
        // Arrange
        await using var sut = new TerminalSessionService();
        var exitedTcs = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.SessionExited += (_, code) => exitedTcs.TrySetResult(code);

        await sut.StartAsync();

        // Act
        await sut.WriteAsync($"exit{Environment.NewLine}");

        // Assert
        var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        completed.Should().Be(exitedTcs.Task);
        sut.IsRunning.Should().BeFalse();
    }
}
