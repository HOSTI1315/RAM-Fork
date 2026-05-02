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
    public string LauncherReferer { get; init; } = "https://www.roblox.com/games/1/Any";
    public string UserAgent { get; init; } = "RobloxStudio/WinInet";

    public TimeSpan PresenceBatchWindow { get; init; } = TimeSpan.FromMilliseconds(50);
    public int PresenceBatchMaxSize { get; init; } = 100;

    public TimeSpan ThumbnailBatchWindow { get; init; } = TimeSpan.FromMilliseconds(50);
    public int ThumbnailBatchMaxSize { get; init; } = 100;
}
