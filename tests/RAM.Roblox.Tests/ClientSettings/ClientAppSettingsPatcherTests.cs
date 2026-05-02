using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.ClientSettings;

namespace RAM.Roblox.Tests.ClientSettings;

public class ClientAppSettingsPatcherTests
{
    [Fact]
    public async Task Apply_writes_json_for_BottingBot_profile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ram-cas-{Guid.NewGuid():N}.json");
        try
        {
            var patcher = new ClientAppSettingsPatcher(NullLogger<ClientAppSettingsPatcher>.Instance);
            await patcher.ApplyAsync(path, BotProfile.BottingBot);

            var json = JsonNode.Parse(await File.ReadAllTextAsync(path)) as JsonObject;
            Assert.NotNull(json);
            Assert.Equal(15, (int)json!["DFIntTaskSchedulerTargetFps"]!);
            Assert.True((bool)json["FFlagDisablePostFx"]!);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Apply_merges_with_existing_user_flags()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ram-cas-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path,
                "{\"FFlagUserOverride\":true,\"DFIntTaskSchedulerTargetFps\":120}");
            var patcher = new ClientAppSettingsPatcher(NullLogger<ClientAppSettingsPatcher>.Instance);
            await patcher.ApplyAsync(path, BotProfile.Normal);

            var json = JsonNode.Parse(await File.ReadAllTextAsync(path)) as JsonObject;
            Assert.NotNull(json);
            Assert.True((bool)json!["FFlagUserOverride"]!);                  // preserved
            Assert.Equal(240, (int)json["DFIntTaskSchedulerTargetFps"]!);    // overridden
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Profile_for_normal_includes_fps_unlock()
    {
        var profile = ClientAppSettingsPatcher.GetProfile(BotProfile.Normal);
        Assert.True(profile.ContainsKey("DFIntTaskSchedulerTargetFps"));
    }

    [Fact]
    public void Profile_for_bot_disables_postfx_and_grass()
    {
        var profile = ClientAppSettingsPatcher.GetProfile(BotProfile.BottingBot);
        Assert.True(profile.ContainsKey("FFlagDisablePostFx"));
        Assert.True(profile.ContainsKey("FIntFRMMaxGrassDistance"));
    }
}
