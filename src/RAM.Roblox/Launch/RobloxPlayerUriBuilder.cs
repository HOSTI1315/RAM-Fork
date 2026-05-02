using RAM.Core.Abstractions;

namespace RAM.Roblox.Launch;

/// <summary>
/// Builds the <c>roblox-player://</c> deep-link URI handed off to RobloxPlayerLauncher.
/// Supports standard place, server-specific, follow-user, and private-server modes.
/// </summary>
public static class RobloxPlayerUriBuilder
{
    private const string AssetGameBaseUrl = "https://assetgame.roblox.com/Game/PlaceLauncher.ashx";

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

        return string.Join('+',
            "roblox-player://1",
            "launchmode:play",
            $"gameinfo:{p.AuthTicket}",
            $"launchtime:{ms}",
            $"placelauncherurl:{encoded}",
            $"browsertrackerid:{p.BrowserTrackerId}",
            $"robloxLocale:{p.Locale}",
            $"gameLocale:{p.Locale}");
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
