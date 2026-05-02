namespace RAM.Roblox.Launch;

public static class BrowserTrackerId
{
    /// <summary>
    /// 16-digit numeric ID embedded in the roblox-player URI so a launched window can be
    /// matched back to the source account by inspecting the launcher's process command line.
    /// </summary>
    public static string Generate() =>
        Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
}
