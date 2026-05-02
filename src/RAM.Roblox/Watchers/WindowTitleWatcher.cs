using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace RAM.Roblox.Watchers;

/// <summary>
/// Polls Roblox process window titles for crash/disconnect markers. Used as a backup
/// signal when the FLog watcher misses (e.g. log file rotated).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowTitleWatcher : IDisposable
{
    public static readonly string[] CrashedTitlePatterns =
    [
        "Disconnected", "Connection Error", "No Connection",
    ];

    private readonly int _processId;
    private readonly TimeSpan _interval;
    private readonly ILogger<WindowTitleWatcher> _logger;
    private CancellationTokenSource? _cts;

    public event EventHandler<string>? CrashDetected;

    public WindowTitleWatcher(int processId, ILogger<WindowTitleWatcher> logger, TimeSpan? interval = null)
    {
        _processId = processId;
        _interval = interval ?? TimeSpan.FromSeconds(5);
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        return Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var proc = Process.GetProcessById(_processId);
                if (proc.HasExited) return;
                var title = proc.MainWindowTitle;
                var matched = CrashedTitlePatterns.FirstOrDefault(p =>
                    title.Contains(p, StringComparison.OrdinalIgnoreCase));
                if (matched is not null)
                {
                    _logger.LogInformation("Window-title watcher detected crash marker '{Marker}' on pid {Pid}",
                        matched, _processId);
                    CrashDetected?.Invoke(this, matched);
                    return;
                }
            }
            catch (ArgumentException) { return; }      // process exited
            catch (InvalidOperationException) { return; }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "WindowTitleWatcher poll error");
            }
            await Task.Delay(_interval, ct);
        }
    }

    public static bool IsCrashedTitle(string title) =>
        CrashedTitlePatterns.Any(p => title.Contains(p, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
