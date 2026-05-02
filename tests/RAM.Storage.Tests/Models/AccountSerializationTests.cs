using System.Text.Json;
using RAM.Core.Models;

namespace RAM.Storage.Tests.Models;

public class AccountSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Account_roundtrips_through_json()
    {
        var account = new Account
        {
            UserId = 123456,
            Username = "alt_player_42",
            DisplayName = "Player 42",
            Cookie = "_|WARNING:-DO-NOT-SHARE-THIS|_X",
            Group = "01 Farming",
            Alias = "main farmer",
            Description = "primary farming alt",
            Tags = ["farming", "active"],
            BrowserTrackerId = "1234567890123456",
            Fields = new Dictionary<string, string> { ["lastPlace"] = "606849621" },
            WindowPlacement = new WindowPlacement(100, 200, 800, 600),
            PinHash = "argon2id$...",
            PinUnlockedUntil = DateTimeOffset.UtcNow.AddMinutes(5),
            Disabled = false,
            Created = DateTimeOffset.UtcNow,
            LastUsed = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(account, Options);
        var roundTripped = JsonSerializer.Deserialize<Account>(json, Options);

        Assert.NotNull(roundTripped);
        Assert.Equal(account.UserId, roundTripped!.UserId);
        Assert.Equal(account.Username, roundTripped.Username);
        Assert.Equal(account.Cookie, roundTripped.Cookie);
        Assert.Equal(account.Group, roundTripped.Group);
        Assert.Equal(account.Tags, roundTripped.Tags);
        Assert.Equal(account.Fields["lastPlace"], roundTripped.Fields["lastPlace"]);
        Assert.Equal(account.WindowPlacement, roundTripped.WindowPlacement);
        Assert.Equal(account.BrowserTrackerId, roundTripped.BrowserTrackerId);
    }

    [Fact]
    public void Account_with_minimal_required_fields_serializes()
    {
        var account = new Account
        {
            UserId = 1,
            Username = "u",
            Cookie = "c",
        };

        var json = JsonSerializer.Serialize(account, Options);
        var roundTripped = JsonSerializer.Deserialize<Account>(json, Options);

        Assert.NotNull(roundTripped);
        Assert.Equal(1ul, roundTripped!.UserId);
        Assert.Empty(roundTripped.Tags);
        Assert.Empty(roundTripped.Fields);
        Assert.Null(roundTripped.WindowPlacement);
    }

    [Fact]
    public void AppSettings_defaults_serialize_with_schema_version_1()
    {
        var settings = new AppSettings();
        var json = JsonSerializer.Serialize(settings, Options);

        Assert.Contains("\"schemaVersion\":1", json);

        var roundTripped = JsonSerializer.Deserialize<AppSettings>(json, Options);
        Assert.NotNull(roundTripped);
        Assert.Equal(1, roundTripped!.SchemaVersion);
        Assert.Equal(BotProfile.Normal, roundTripped.DefaultProfile);
        Assert.Equal(8, roundTripped.BackupRetentionHours);
    }

    [Fact]
    public void RecentGame_roundtrips()
    {
        var game = new RecentGame
        {
            PlaceId = 606849621,
            UniverseId = 220851708,
            Name = "Jailbreak",
            Region = "US-East",
        };
        var json = JsonSerializer.Serialize(game, Options);
        var rt = JsonSerializer.Deserialize<RecentGame>(json, Options);
        Assert.Equal(game.PlaceId, rt!.PlaceId);
        Assert.Equal(game.UniverseId, rt.UniverseId);
    }
}
