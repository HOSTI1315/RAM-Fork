using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Auth;
using RAM.Roblox.ClientSettings;

namespace RAM.Roblox.Launch;

/// <summary>
/// Orchestrates a full Roblox client launch:
/// <list type="number">
///   <item>Engages <see cref="SingletonMutexBypass"/> + <see cref="CookieFileLock"/>.</item>
///   <item>Acquires an auth ticket via <see cref="AuthTicketProvider"/>.</item>
///   <item>Patches <c>ClientAppSettings.json</c> for the requested <see cref="BotProfile"/>.</item>
///   <item>Builds the <c>roblox-player://</c> URI and hands it to the OS via shell-execute.</item>
///   <item>Returns the launched process info (best-effort match by browser-tracker-id).</item>
/// </list>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RobloxLauncher : ILauncher
{
    private readonly AuthTicketProvider _ticketProvider;
    private readonly ClientAppSettingsPatcher _settingsPatcher;
    private readonly SingletonMutexBypass _mutex;
    private readonly CookieFileLock _cookieLock;
    private readonly RobloxInstallLocator _installLocator;
    private readonly ILogger<RobloxLauncher> _logger;

    public RobloxLauncher(
        AuthTicketProvider ticketProvider,
        ClientAppSettingsPatcher settingsPatcher,
        SingletonMutexBypass mutex,
        CookieFileLock cookieLock,
        RobloxInstallLocator installLocator,
        ILogger<RobloxLauncher> logger)
    {
        _ticketProvider = ticketProvider;
        _settingsPatcher = settingsPatcher;
        _mutex = mutex;
        _cookieLock = cookieLock;
        _installLocator = installLocator;
        _logger = logger;
    }

    /// <summary>How long to keep the cookie file lock held after the URI is handed off
    /// to the OS shell, so the Roblox launcher has time to read cookies.</summary>
    public TimeSpan PostLaunchHoldTime { get; init; } = TimeSpan.FromSeconds(3);

    public async Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default)
    {
        var userId = request.Account.UserId;
        _logger.LogInformation("Launch requested for {UserId} target={Target}", userId, request.Target);

        if (string.IsNullOrEmpty(request.Account.Cookie))
        {
            _logger.LogWarning("Launch aborted for {UserId}: account has no cookie", userId);
            return LaunchResult.Fail("Account has no cookie. Re-add the account.");
        }

        try
        {
            _logger.LogDebug("Acquiring singleton mutex bypass for {UserId}", userId);
            _mutex.Acquire();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mutex bypass failed for {UserId}", userId);
            return LaunchResult.Fail($"Could not acquire singleton mutex bypass: {ex.Message}");
        }

        IDisposable? cookieLockHandle = null;
        try
        {
            _logger.LogDebug("Acquiring cookie file lock for {UserId}", userId);
            cookieLockHandle = await _cookieLock.AcquireAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cookie file lock failed for {UserId}", userId);
            return LaunchResult.Fail($"Could not lock Roblox cookie file: {ex.Message}");
        }

        try
        {
            _logger.LogDebug("Fetching auth ticket for {UserId}", userId);
            string ticket;
            try
            {
                ticket = await _ticketProvider.GetAsync(request.Account.Cookie, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auth ticket fetch failed for {UserId}", userId);
                return LaunchResult.Fail(
                    "Could not fetch Roblox auth ticket. The cookie may have expired or been revoked. " +
                    "Re-auth via Add Account.");
            }
            if (string.IsNullOrWhiteSpace(ticket))
            {
                _logger.LogError("Auth ticket fetch returned empty for {UserId}", userId);
                return LaunchResult.Fail("Roblox returned an empty auth ticket. Cookie may be invalid.");
            }
            _logger.LogDebug("Auth ticket fetched for {UserId} (len={Len})", userId, ticket.Length);

            var trackerId = string.IsNullOrEmpty(request.Account.BrowserTrackerId)
                ? BrowserTrackerId.Generate()
                : request.Account.BrowserTrackerId;

            var installPath = _installLocator.FindCurrentVersion();
            if (installPath is not null)
            {
                _logger.LogDebug("Roblox install found at {Path}; patching ClientAppSettings", installPath);
                var settingsPath = Path.Combine(installPath, "ClientSettings", "ClientAppSettings.json");
                try { await _settingsPatcher.ApplyAsync(settingsPath, request.Profile, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "ClientAppSettings patch failed (non-fatal)"); }
            }
            else
            {
                _logger.LogWarning("Roblox install not found under %LocalAppData%\\Roblox\\Versions. " +
                                   "Is Roblox installed via the standard launcher / Bloxstrap?");
                // Don't fail — Roblox launcher may still resolve via the URI scheme handler.
            }

            var uri = RobloxPlayerUriBuilder.Build(new RobloxPlayerUriBuilder.Params(
                AuthTicket: ticket,
                BrowserTrackerId: trackerId,
                Target: request.Target));
            _logger.LogDebug("Built launch URI for {UserId} length={Len}", userId, uri.Length);

            int pid;
            try
            {
                pid = ShellLaunch(uri);
            }
            catch (System.ComponentModel.Win32Exception wex)
            {
                _logger.LogError(wex, "Shell-execute failed for roblox-player URI (handler not registered?)");
                return LaunchResult.Fail(
                    "Windows could not handle the roblox-player:// URI. " +
                    "This usually means Roblox isn't installed properly. " +
                    "Reinstall Roblox or run it once from the official site, then try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Shell-execute failed for {UserId}", userId);
                return LaunchResult.Fail($"Could not launch Roblox: {ex.Message}");
            }

            _logger.LogInformation("Launch dispatched for {UserId} (shell-exec pid={Pid})", userId, pid);

            // Hold the cookie lock until the Roblox launcher has had a chance to read it.
            // Subsequent parallel launches wait on AcquireAsync until this delay elapses.
            await Task.Delay(PostLaunchHoldTime, ct);

            return LaunchResult.Ok(pid, trackerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launch failed unexpectedly for {UserId}", userId);
            return LaunchResult.Fail(ex.Message);
        }
        finally
        {
            cookieLockHandle?.Dispose();
        }
    }

    /// <summary>
    /// Hands the URI to the OS shell via <c>cmd /C start "" "&lt;uri&gt;"</c>. This matches
    /// what Fork-4 and ReJoin do — the <c>start</c> built-in is more forgiving with
    /// custom URI schemes than .NET's <c>Process.Start</c> with <c>UseShellExecute=true</c>,
    /// which has had subtle behaviour changes between .NET Framework, .NET Core, and
    /// .NET 5+. Returns the cmd.exe pid (Roblox launcher itself runs out-of-tree).
    /// </summary>
    private static int ShellLaunch(string uri)
    {
        // The first "" is the title argument expected by the Windows `start` command —
        // when its first quoted argument is the URI, `start` would interpret it as the
        // window title and silently do nothing. Always pass an empty title first.
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/C start \"\" \"{uri}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        using var proc = Process.Start(psi);
        return proc?.Id ?? -1;
    }
}
