using RAM.Roblox.Launch;

namespace RAM.Roblox.Tests.Launch;

public class BrowserTrackerIdTests
{
    [Fact]
    public void Generated_id_is_16_digits_numeric()
    {
        var id = BrowserTrackerId.Generate();
        Assert.Equal(16, id.Length);
        Assert.All(id, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void Multiple_generations_produce_distinct_values()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => BrowserTrackerId.Generate()).ToHashSet();
        Assert.Equal(50, ids.Count);
    }
}
