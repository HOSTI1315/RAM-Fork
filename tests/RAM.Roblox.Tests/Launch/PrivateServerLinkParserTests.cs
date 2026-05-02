using RAM.Roblox.Launch;

namespace RAM.Roblox.Tests.Launch;

public class PrivateServerLinkParserTests
{
    [Theory]
    [InlineData("https://www.roblox.com/games/606849621/Jailbreak?privateServerLinkCode=ABC-123-XYZ", "ABC-123-XYZ")]
    [InlineData("https://www.roblox.com/share?code=NEW-format-CODE&type=Server", "NEW-format-CODE")]
    [InlineData("?code=just-a-code", "just-a-code")]
    public void Extracts_link_code_from_known_formats(string url, string expected)
    {
        Assert.Equal(expected, PrivateServerLinkParser.TryExtractCode(url));
    }

    [Fact]
    public void Returns_null_when_no_code_present()
    {
        Assert.Null(PrivateServerLinkParser.TryExtractCode("https://www.roblox.com/games/606849621/Jailbreak"));
        Assert.Null(PrivateServerLinkParser.TryExtractCode(""));
        Assert.Null(PrivateServerLinkParser.TryExtractCode("not a url"));
    }

    [Fact]
    public void Legacy_format_takes_precedence_over_modern_pattern()
    {
        // If both patterns are present somehow, legacy wins (it's more specific).
        var url = "https://r.com/x?privateServerLinkCode=LEGACY&code=MODERN";
        Assert.Equal("LEGACY", PrivateServerLinkParser.TryExtractCode(url));
    }
}
