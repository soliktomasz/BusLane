namespace BusLane.Services.Terminal;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Process-backed terminal session service. Uses script(1) on Unix when available to get PTY behavior.
/// </summary>
public sealed class TerminalSessionService : ITerminalSessionService
{
    private const int ShutdownTimeoutMilliseconds = 2000;
    private const int ReadBufferSize = 4096;
    private readonly object _sync = new();

    private Process? _process;
    private CancellationTokenSource? _ioCts;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private bool _disposed;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int?>? SessionExited;

    public Guid SessionId { get; private set; }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _process is { HasExited: false };
            }
        }
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            ThrowIfDisposed();
            if (_process is { HasExited: false })
            {
                return Task.CompletedTask;
            }
        }

        var process = new Process
        {
            StartInfo = BuildProcessStartInfo(),
            EnableRaisingEvents = true
        };
        process.Exited += OnProcessExited;

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Failed to start terminal process.");
        }

        process.StandardInput.AutoFlush = true;

        var cts = new CancellationTokenSource();
        var stdoutTask = PumpOutputAsync(process.StandardOutput, cts.Token);
        var stderrTask = PumpOutputAsync(process.StandardError, cts.Token);

        lock (_sync)
        {
            _process = process;
            _ioCts = cts;
            _stdoutTask = stdoutTask;
            _stderrTask = stderrTask;
            SessionId = Guid.NewGuid();
        }

        return Task.CompletedTask;
    }

    public async Task WriteAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Process? process;
        lock (_sync)
        {
            ThrowIfDisposed();
            process = _process;
        }

        if (process is null || process.HasExited)
        {
            return;
        }

        await process.StandardInput.WriteAsync(text.AsMemory(), ct).ConfigureAwait(false);
        await process.StandardInput.FlushAsync().ConfigureAwait(false);
    }

    public Task ResizeAsync(int cols, int rows, CancellationToken ct = default)
    {
        _ = cols;
        _ = rows;
        _ = ct;
        return Task.CompletedTask;
    }

    public async Task RestartAsync(CancellationToken ct = default)
    {
        await StopInternalAsync(ct).ConfigureAwait(false);
        await StartAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopInternalAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
    }

    private async Task StopInternalAsync(CancellationToken ct)
    {
        Process? process;
        CancellationTokenSource? ioCts;
        Task? stdoutTask;
        Task? stderrTask;

        lock (_sync)
        {
            process = _process;
            ioCts = _ioCts;
            stdoutTask = _stdoutTask;
            stderrTask = _stderrTask;

            _process = null;
            _ioCts = null;
            _stdoutTask = null;
            _stderrTask = null;
        }

        if (process == null)
        {
            ioCts?.Dispose();
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                try
                {
                    await process.StandardInput.WriteLineAsync("exit").ConfigureAwait(false);
                    await process.StandardInput.FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore stdin failures during shutdown.
                }
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ShutdownTimeoutMilliseconds);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Ignore kill failures if process is already gone.
                    }
                }
            }
        }
        finally
        {
            ioCts?.Cancel();
            await AwaitTaskSafeAsync(stdoutTask).ConfigureAwait(false);
            await AwaitTaskSafeAsync(stderrTask).ConfigureAwait(false);
            ioCts?.Dispose();

            process.Exited -= OnProcessExited;
            process.Dispose();
        }
    }

    private async Task PumpOutputAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[ReadBufferSize];

        while (!ct.IsCancellationRequested)
        {
            int count;
            try
            {
                count = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                break;
            }

            if (count <= 0)
            {
                break;
            }

            OutputReceived?.Invoke(this, new string(buffer, 0, count));
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (sender is not Process process)
        {
            return;
        }

        int? exitCode = null;
        try
        {
            exitCode = process.ExitCode;
        }
        catch
        {
            // Ignore exit-code read failures.
        }

        SessionExited?.Invoke(this, exitCode);
    }

    private ProcessStartInfo BuildProcessStartInfo()
    {
        var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            workingDirectory = Environment.CurrentDirectory;
        }

        if (OperatingSystem.IsWindows())
        {
            return BuildWindowsProcessStartInfo(workingDirectory);
        }

        var shellPath = ResolveUnixShell();
        if (TryBuildUnixPtyStartInfo(shellPath, workingDirectory, out var ptyInfo))
        {
            return ptyInfo;
        }

        var fallbackInfo = CreateBaseStartInfo(shellPath, workingDirectory);
        fallbackInfo.ArgumentList.Add("-i");
        return fallbackInfo;
    }

    private ProcessStartInfo BuildWindowsProcessStartInfo(string workingDirectory)
    {
        var shellPath = ResolveExecutable(["pwsh.exe", "powershell.exe", "cmd.exe"]) ?? "cmd.exe";
        var startInfo = CreateBaseStartInfo(shellPath, workingDirectory);

        var fileName = Path.GetFileName(shellPath);
        if (fileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("/Q");
        }
        else
        {
            startInfo.ArgumentList.Add("-NoLogo");
        }

        return startInfo;
    }

    private bool TryBuildUnixPtyStartInfo(string shellPath, string workingDirectory, out ProcessStartInfo info)
    {
        info = default!;

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        var scriptPath = ResolveExecutable(["script"]);
        if (scriptPath == null)
        {
            return false;
        }

        info = CreateBaseStartInfo(scriptPath, workingDirectory);
        info.Environment["TERM"] = "xterm-256color";

        if (OperatingSystem.IsMacOS())
        {
            info.ArgumentList.Add("-q");
            info.ArgumentList.Add("/dev/null");
            info.ArgumentList.Add(shellPath);
            info.ArgumentList.Add("-i");
            return true;
        }

        info.ArgumentList.Add("-q");
        info.ArgumentList.Add("-f");
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add($"{shellPath} -i");
        info.ArgumentList.Add("/dev/null");
        return true;
    }

    private static ProcessStartInfo CreateBaseStartInfo(string fileName, string workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private static string ResolveUnixShell()
    {
        var shell = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrWhiteSpace(shell))
        {
            return shell;
        }

        return "/bin/bash";
    }

    private static string? ResolveExecutable(IEnumerable<string> candidates)
    {
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IEnumerable<string> extensions = [string.Empty];
        if (OperatingSystem.IsWindows())
        {
            extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ext => ext.StartsWith('.') ? ext : $".{ext}");
        }

        foreach (var candidate in candidates)
        {
            if (Path.IsPathRooted(candidate))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                continue;
            }

            foreach (var entry in pathEntries)
            {
                if (OperatingSystem.IsWindows())
                {
                    foreach (var ext in extensions)
                    {
                        var fullPath = Path.Combine(entry, candidate);
                        if (!fullPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        {
                            fullPath += ext;
                        }

                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }
                else
                {
                    var fullPath = Path.Combine(entry, candidate);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
        }

        return null;
    }

    private static async Task AwaitTaskSafeAsync(Task? task)
    {
        if (task == null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Ignore pump failures during shutdown.
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TerminalSessionService));
        }
    }
}
