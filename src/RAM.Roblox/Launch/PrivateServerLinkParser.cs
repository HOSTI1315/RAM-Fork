using System.Text.RegularExpressions;

namespace RAM.Roblox.Launch;

/// <summary>
/// Parses both legacy and modern Roblox private-server share links to extract the
/// link code. ReJoin contributed this logic — it handles old <c>?privateServerLinkCode=</c>
/// URLs and new <c>/share?code=...</c> shape transparently.
/// </summary>
public static partial class PrivateServerLinkParser
{
    [GeneratedRegex(@"privateServerLinkCode=([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyCodePattern();

    [GeneratedRegex(@"[?&]code=([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ModernCodePattern();

    public static string? TryExtractCode(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var legacy = LegacyCodePattern().Match(url);
        if (legacy.Success) return legacy.Groups[1].Value;
        var modern = ModernCodePattern().Match(url);
        if (modern.Success) return modern.Groups[1].Value;
        return null;
    }
}
