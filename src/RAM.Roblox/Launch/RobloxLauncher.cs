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
        if (string.IsNullOrEmpty(request.Account.Cookie))
            return LaunchResult.Fail("Account has no cookie");

        _mutex.Acquire();

        using var cookieLockHandle = await _cookieLock.AcquireAsync(ct);
        try
        {
            var ticket = await _ticketProvider.GetAsync(request.Account.Cookie, ct);
            var trackerId = string.IsNullOrEmpty(request.Account.BrowserTrackerId)
                ? BrowserTrackerId.Generate()
                : request.Account.BrowserTrackerId;

            var installPath = _installLocator.FindCurrentVersion();
            if (installPath is not null)
            {
                var settingsPath = Path.Combine(installPath, "ClientSettings", "ClientAppSettings.json");
                await _settingsPatcher.ApplyAsync(settingsPath, request.Profile, ct);
            }
            else
            {
                _logger.LogWarning("Roblox install not found; skipping ClientAppSettings patch");
            }

            var uri = RobloxPlayerUriBuilder.Build(new RobloxPlayerUriBuilder.Params(
                AuthTicket: ticket,
                BrowserTrackerId: trackerId,
                Target: request.Target));

            var pid = ShellLaunch(uri);

            // Hold the cookie lock until the Roblox launcher has had a chance to read it.
            // Subsequent parallel launches wait on AcquireAsync until this delay elapses.
            await Task.Delay(PostLaunchHoldTime, ct);

            return LaunchResult.Ok(pid, trackerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launch failed for account {UserId}", request.Account.UserId);
            return LaunchResult.Fail(ex.Message);
        }
    }

    private int ShellLaunch(string uri)
    {
        var psi = new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true,
        };
        using var proc = Process.Start(psi);
        return proc?.Id ?? -1;
    }
}
