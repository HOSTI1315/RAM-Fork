using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RAM.Roblox.Watchers;

/// <summary>
/// Tails a Roblox player.log and emits <see cref="GameState"/> transitions parsed from
/// FLog markers. More robust than window-title polling for detecting connect/disconnect.
/// Search order for log files: <c>%LocalAppData%\Roblox\logs</c> →
/// <c>%LocalAppData%\Bloxstrap\Logs</c> → process MainModule directory.
/// </summary>
public sealed partial class FlogWatcher : IDisposable
{
    public enum GameState { Unknown, Initializing, Connected, Paused, Disconnected, BetaMenu, Crashed }

    public static readonly string[] DefaultLogDirectories =
    [
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs"),
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bloxstrap", "Logs"),
    ];

    [GeneratedRegex(@"\[FLog::DataModel\].*?(initializing|paused|disconnect|leaving)", RegexOptions.IgnoreCase)]
    private static partial Regex DataModelPattern();

    [GeneratedRegex(@"\[FLog::SingleSurfaceApp\].*?(beta menu|exit|loading)", RegexOptions.IgnoreCase)]
    private static partial Regex AppPattern();

    [GeneratedRegex(@"\[FLog::Network\].*?(0x10[0-9a-f]+|connection lost)", RegexOptions.IgnoreCase)]
    private static partial Regex NetworkErrorPattern();

    private readonly string _logFilePath;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger<FlogWatcher> _logger;
    private long _readPosition;
    private CancellationTokenSource? _cts;
    private GameState _state = GameState.Unknown;

    public event EventHandler<GameState>? StateChanged;
    public GameState State => _state;
    public string LogFilePath => _logFilePath;

    public FlogWatcher(string logFilePath, ILogger<FlogWatcher> logger, TimeSpan? pollInterval = null)
    {
        _logFilePath = logFilePath;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    /// <summary>Resolves the most recent FLog file for a process (by main-module dir).</summary>
    public static string? FindLogFor(int processId)
    {
        var candidates = new List<string>();
        foreach (var dir in DefaultLogDirectories)
            if (Directory.Exists(dir))
                candidates.AddRange(Directory.GetFiles(dir, "*.log", SearchOption.TopDirectoryOnly));

        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById(processId);
            var moduleDir = System.IO.Path.GetDirectoryName(proc.MainModule?.FileName);
            if (!string.IsNullOrEmpty(moduleDir) && Directory.Exists(moduleDir))
                candidates.AddRange(Directory.GetFiles(moduleDir, "*.log", SearchOption.AllDirectories));
        }
        catch { /* process exited or access denied */ }

        return candidates
            .Distinct()
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .FirstOrDefault();
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        return Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        if (File.Exists(_logFilePath))
            _readPosition = new FileInfo(_logFilePath).Length;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    if (fs.Length > _readPosition)
                    {
                        fs.Seek(_readPosition, SeekOrigin.Begin);
                        using var reader = new StreamReader(fs);
                        var newContent = await reader.ReadToEndAsync(ct);
                        ProcessLines(newContent);
                        _readPosition = fs.Length;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogTrace(ex, "FlogWatcher read error (will retry)");
            }
            await Task.Delay(_pollInterval, ct);
        }
    }

    private void ProcessLines(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var dm = DataModelPattern().Match(line);
            if (dm.Success)
            {
                var verb = dm.Groups[1].Value.ToLowerInvariant();
                ChangeState(verb switch
                {
                    "initializing" => GameState.Initializing,
                    "paused" => GameState.Paused,
                    "disconnect" or "leaving" => GameState.Disconnected,
                    _ => _state,
                });
                continue;
            }
            var app = AppPattern().Match(line);
            if (app.Success && app.Groups[1].Value.Equals("beta menu", StringComparison.OrdinalIgnoreCase))
            {
                ChangeState(GameState.BetaMenu);
                continue;
            }
            if (NetworkErrorPattern().IsMatch(line))
            {
                ChangeState(GameState.Disconnected);
                continue;
            }
            // Heuristic: presence of "DataModel" + "playing" implies Connected
            if (line.Contains("[FLog::DataModel]", StringComparison.Ordinal) &&
                line.Contains("loaded", StringComparison.OrdinalIgnoreCase))
            {
                ChangeState(GameState.Connected);
            }
        }
    }

    private void ChangeState(GameState next)
    {
        if (next == _state) return;
        _state = next;
        StateChanged?.Invoke(this, next);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
