using RAM.Core.Abstractions;
using RAM.Roblox.Launch;

namespace RAM.Roblox.Tests.Launch;

public class RobloxPlayerUriBuilderTests
{
    [Fact]
    public void Builds_uri_for_standard_place_launch()
    {
        var uri = RobloxPlayerUriBuilder.Build(new RobloxPlayerUriBuilder.Params(
            AuthTicket: "TKT-1",
            BrowserTrackerId: "1234567890123456",
            Target: new LaunchTarget.Place(606849621),
            LaunchTime: DateTimeOffset.FromUnixTimeMilliseconds(1700000000000)));

        Assert.StartsWith("roblox-player://1+launchmode:play+gameinfo:TKT-1+", uri);
        Assert.Contains("launchtime:1700000000000", uri);
        Assert.Contains("browsertrackerid:1234567890123456", uri);
        Assert.Contains("placelauncherurl:", uri);
        Assert.Contains(Uri.EscapeDataString("placeId=606849621"), uri);
        Assert.Contains(Uri.EscapeDataString("request=RequestGame"), uri);
    }

    [Fact]
    public void Place_with_jobid_uses_RequestGameJob()
    {
        var url = RobloxPlayerUriBuilder.BuildPlaceLauncherUrl(
            new LaunchTarget.Place(606849621, "abc-123"),
            "TRK");
        Assert.Contains("request=RequestGameJob", url);
        Assert.Contains("gameId=abc-123", url);
        Assert.Contains("placeId=606849621", url);
    }

    [Fact]
    public void Follow_user_emits_RequestFollowUser()
    {
        var url = RobloxPlayerUriBuilder.BuildPlaceLauncherUrl(
            new LaunchTarget.FollowUser(42), "TRK");
        Assert.Contains("request=RequestFollowUser", url);
        Assert.Contains("userId=42", url);
    }

    [Fact]
    public void Private_server_emits_RequestPrivateGame_with_link_code()
    {
        var url = RobloxPlayerUriBuilder.BuildPlaceLauncherUrl(
            new LaunchTarget.PrivateServer(606849621, "PSCODE-XYZ"), "TRK");
        Assert.Contains("request=RequestPrivateGame", url);
        Assert.Contains("placeId=606849621", url);
        Assert.Contains("accessCode=PSCODE-XYZ", url);
        Assert.Contains("linkCode=PSCODE-XYZ", url);
    }
}
