using RAM.Roblox.Watchers;

namespace RAM.Roblox.Tests.Watchers;

public class WindowTitleWatcherTests
{
    [Theory]
    [InlineData("Roblox", false)]
    [InlineData("Roblox - Disconnected", true)]
    [InlineData("Connection Error", true)]
    [InlineData("No Connection", true)]
    [InlineData("DISCONNECTED", true)]
    public void IsCrashedTitle_matches_known_patterns(string title, bool expected)
    {
        Assert.Equal(expected, WindowTitleWatcher.IsCrashedTitle(title));
    }
}
