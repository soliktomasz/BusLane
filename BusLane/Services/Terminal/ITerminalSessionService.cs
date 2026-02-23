namespace BusLane.Services.Terminal;

/// <summary>
/// Provides a single interactive terminal session.
/// </summary>
public interface ITerminalSessionService : IAsyncDisposable
{
    /// <summary>
    /// Raised when new terminal output is available.
    /// </summary>
    event EventHandler<string>? OutputReceived;

    /// <summary>
    /// Raised when the terminal process exits.
    /// </summary>
    event EventHandler<int?>? SessionExited;

    /// <summary>
    /// Gets the active session identifier.
    /// </summary>
    Guid SessionId { get; }

    /// <summary>
    /// Gets whether the terminal process is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the terminal session if not already running.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Writes raw input to the terminal stdin.
    /// </summary>
    Task WriteAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Resizes the terminal. If resizing is unsupported, this is a no-op.
    /// </summary>
    Task ResizeAsync(int cols, int rows, CancellationToken ct = default);

    /// <summary>
    /// Restarts the terminal session.
    /// </summary>
    Task RestartAsync(CancellationToken ct = default);
}
