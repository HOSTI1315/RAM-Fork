namespace RAM.Roblox.Api;

public sealed record RobloxApiOptions
{
    public string AuthBaseUrl { get; init; } = "https://auth.roblox.com";
    public string UsersBaseUrl { get; init; } = "https://users.roblox.com";
    public string PresenceBaseUrl { get; init; } = "https://presence.roblox.com";
    public string ThumbnailsBaseUrl { get; init; } = "https://thumbnails.roblox.com";
    public string GamesBaseUrl { get; init; } = "https://games.roblox.com";
    public string ApisBaseUrl { get; init; } = "https://apis.roblox.com";
    public string EconomyBaseUrl { get; init; } = "https://economy.roblox.com";
    /// <summary>Referer header for auth requests. Must be a real Roblox game URL —
    /// <c>games/1/Any</c> doesn't exist and Roblox can 403 on it. Matches Fork-4's
    /// choice of a popular evergreen game.</summary>
    public string LauncherReferer { get; init; } = "https://www.roblox.com/games/2753915549/Blox-Fruits";

    /// <summary>User-Agent header. Roblox's WAF flags / blocks requests with the
    /// default .NET UA. Chromium-style UA (matching Fork-4) is the safe default.</summary>
    public string UserAgent { get; init; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public TimeSpan PresenceBatchWindow { get; init; } = TimeSpan.FromMilliseconds(50);
    public int PresenceBatchMaxSize { get; init; } = 100;

    public TimeSpan ThumbnailBatchWindow { get; init; } = TimeSpan.FromMilliseconds(50);
    public int ThumbnailBatchMaxSize { get; init; } = 100;
}
