using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace RAM.Roblox.Watchers;

/// <summary>
/// Optional safety net: if a Roblox process drops below a configured memory threshold,
/// it's likely stuck/crashed/zombied and gets killed. Default threshold 200 MB.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MemoryThresholdKiller
{
    private readonly long _thresholdBytes;
    private readonly ILogger<MemoryThresholdKiller> _logger;

    public MemoryThresholdKiller(int thresholdMb, ILogger<MemoryThresholdKiller> logger)
    {
        _thresholdBytes = (long)thresholdMb * 1024 * 1024;
        _logger = logger;
    }

    /// <summary>Returns true if killed.</summary>
    public bool CheckAndKill(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            if (proc.HasExited) return false;
            proc.Refresh();
            if (proc.WorkingSet64 < _thresholdBytes)
            {
                _logger.LogInformation(
                    "Killing pid {Pid}: working set {Mb} MiB below threshold",
                    processId, proc.WorkingSet64 / 1024 / 1024);
                proc.Kill(entireProcessTree: false);
                return true;
            }
        }
        catch (ArgumentException) { /* process gone */ }
        catch (InvalidOperationException) { /* process gone */ }
        return false;
    }
}
