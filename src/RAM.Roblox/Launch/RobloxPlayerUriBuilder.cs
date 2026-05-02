using RAM.Core.Abstractions;

namespace RAM.Roblox.Launch;

/// <summary>
/// Builds the <c>roblox-player:</c> deep-link URI handed off to RobloxPlayerLauncher.
/// Supports standard place, server-specific, follow-user, and private-server modes.
///
/// <para><b>URI format gotchas</b> — confirmed against original RAM
/// (<c>Account.cs:656</c>), Fork-4 (<c>platform/windows/launch.rs:62</c>), and ReJoin
/// (<c>roblox_api.py:106</c>):</para>
/// <list type="bullet">
///   <item>Scheme is <b><c>roblox-player:</c> with a single colon</b>, NOT
///         <c>roblox-player://</c>. The Roblox launcher's argument parser silently
///         bails on the <c>://</c> form.</item>
///   <item>Trailing <c>+channel:+LaunchExp:InApp</c> is required — Roblox treats
///         their absence as malformed and exits without launching.</item>
///   <item>The inner <c>placelauncherurl</c> for <c>RequestGame</c> /
///         <c>RequestGameJob</c> must include <c>&amp;browserTrackerId=</c> — the
///         outer <c>browsertrackerid:</c> alone is not enough.</item>
/// </list>
/// </summary>
public static class RobloxPlayerUriBuilder
{
    private const string AssetGameBaseUrl = "https://assetgame.roblox.com/game/PlaceLauncher.ashx";

    public sealed record Params(
        string AuthTicket,
        string BrowserTrackerId,
        LaunchTarget Target,
        string Locale = "en_us",
        DateTimeOffset? LaunchTime = null);

    public static string Build(Params p)
    {
        var ms = (p.LaunchTime ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();
        var placeLauncherUrl = BuildPlaceLauncherUrl(p.Target, p.BrowserTrackerId);
        var encoded = Uri.EscapeDataString(placeLauncherUrl);

        // NOTE: scheme is `roblox-player:` (single colon, no slashes). See class doc.
        return string.Join('+',
            "roblox-player:1",
            "launchmode:play",
            $"gameinfo:{p.AuthTicket}",
            $"launchtime:{ms}",
            $"placelauncherurl:{encoded}",
            $"browsertrackerid:{p.BrowserTrackerId}",
            $"robloxLocale:{p.Locale}",
            $"gameLocale:{p.Locale}",
            "channel:",
            "LaunchExp:InApp");
    }

    public static string BuildPlaceLauncherUrl(LaunchTarget target, string browserTrackerId) => target switch
    {
        LaunchTarget.Place { JobId: null } pl =>
            $"{AssetGameBaseUrl}?request=RequestGame&browserTrackerId={browserTrackerId}" +
            $"&placeId={pl.PlaceId}&isPlayTogetherGame=false",

        LaunchTarget.Place { JobId: { } jobId } pl =>
            $"{AssetGameBaseUrl}?request=RequestGameJob&browserTrackerId={browserTrackerId}" +
            $"&placeId={pl.PlaceId}&gameId={jobId}&isPlayTogetherGame=false",

        LaunchTarget.FollowUser fu =>
            $"{AssetGameBaseUrl}?request=RequestFollowUser&browserTrackerId={browserTrackerId}" +
            $"&userId={fu.UserId}",

        LaunchTarget.PrivateServer ps =>
            $"{AssetGameBaseUrl}?request=RequestPrivateGame&browserTrackerId={browserTrackerId}" +
            $"&placeId={ps.PlaceId}&accessCode={ps.LinkCode}&linkCode={ps.LinkCode}",

        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown launch target"),
    };
}
