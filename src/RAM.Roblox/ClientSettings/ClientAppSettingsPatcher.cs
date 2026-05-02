using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using RAM.Core.Models;

namespace RAM.Roblox.ClientSettings;

/// <summary>
/// Writes per-profile FastFlag overrides to <c>ClientSettings\ClientAppSettings.json</c>
/// next to the Roblox player executable. Profiles:
/// <list type="bullet">
///   <item><see cref="BotProfile.Normal"/> — modest tweaks (FPS unlock).</item>
///   <item><see cref="BotProfile.BottingPlayer"/> — performance tweaks for human-driven alts.</item>
///   <item><see cref="BotProfile.BottingBot"/> — minimum graphics for headless farming.</item>
/// </list>
/// </summary>
public sealed class ClientAppSettingsPatcher
{
    private readonly ILogger<ClientAppSettingsPatcher> _logger;

    public ClientAppSettingsPatcher(ILogger<ClientAppSettingsPatcher> logger)
    {
        _logger = logger;
    }

    public static IReadOnlyDictionary<string, JsonNode?> GetProfile(BotProfile profile) => profile switch
    {
        BotProfile.Normal => new Dictionary<string, JsonNode?>
        {
            ["DFIntTaskSchedulerTargetFps"] = 240,
            ["FFlagDebugGraphicsPreferD3D11"] = true,
        },
        BotProfile.BottingPlayer => new Dictionary<string, JsonNode?>
        {
            ["DFIntTaskSchedulerTargetFps"] = 60,
            ["FFlagDebugGraphicsPreferD3D11"] = true,
            ["FFlagHandleAltEnterFullscreenManually"] = false,
        },
        BotProfile.BottingBot => new Dictionary<string, JsonNode?>
        {
            ["DFIntTaskSchedulerTargetFps"] = 15,
            ["DFIntDebugFRMQualityLevelOverride"] = 1,
            ["FFlagDebugGraphicsPreferD3D11"] = true,
            ["FFlagDisablePostFx"] = true,
            ["FIntFRMMaxGrassDistance"] = 0,
            ["FIntRenderShadowIntensity"] = 0,
        },
        _ => new Dictionary<string, JsonNode?>(),
    };

    /// <summary>
    /// Writes the profile flags to <paramref name="targetFile"/>, merging with any existing
    /// JSON object to preserve user-set flags.
    /// </summary>
    public async Task ApplyAsync(string targetFile, BotProfile profile, CancellationToken ct = default)
    {
        var dir = System.IO.Path.GetDirectoryName(targetFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        JsonObject root;
        if (File.Exists(targetFile))
        {
            try
            {
                var existing = await File.ReadAllTextAsync(targetFile, ct);
                root = JsonNode.Parse(existing) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                _logger.LogWarning("Existing ClientAppSettings.json is invalid; overwriting");
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        foreach (var (key, value) in GetProfile(profile))
            root[key] = value?.DeepClone();

        var output = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(targetFile, output, ct);
        _logger.LogDebug("Wrote ClientAppSettings.json profile {Profile} → {Path}", profile, targetFile);
    }
}
