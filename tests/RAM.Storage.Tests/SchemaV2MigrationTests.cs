using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RAM.Core.Models;
using RAM.Storage;
using RAM.Storage.Crypto;
using RAM.Storage.Json;

namespace RAM.Storage.Tests;

/// <summary>
/// schema_version 1 → 2 migration tests for the Proxy field addition.
/// </summary>
public class SchemaV2MigrationTests
{
    private static AccountStore BuildStore(string root, string password) =>
        new(
            new RamDataDirectory(root),
            new SodiumArgon2idCipher(),
            new Argon2iLegacyReader(),
            new DpapiFallback(),
            () => password,
            NullLogger<AccountStore>.Instance);

    private static byte[] EncryptV1Envelope(string password)
    {
        // v1 envelope: schema_version 1, no `proxy` field on any account.
        var v1Json = """
            {
              "schemaVersion": 1,
              "kdfVariant": "argon2id",
              "accounts": [
                { "userId": 1, "username": "alpha", "displayName": "",
                  "cookie": "C1", "group": "01 Farming", "alias": "main",
                  "description": "", "tags": [], "browserTrackerId": "BT1",
                  "fields": {}, "windowPlacement": null,
                  "pinHash": null, "pinUnlockedUntil": null,
                  "disabled": false,
                  "created": "2025-01-01T00:00:00+00:00",
                  "lastUsed": null }
              ],
              "recentGames": []
            }
            """u8.ToArray();
        var cipher = new SodiumArgon2idCipher();
        return cipher.Encrypt(v1Json, password);
    }

    [Fact]
    public async Task A_reading_v1_file_produces_account_with_Proxy_null()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "AccountData.json");
        await File.WriteAllBytesAsync(path, EncryptV1Envelope("pw"));

        var store = BuildStore(tmp.Path, "pw");
        var loaded = await store.LoadAllAsync();

        Assert.Single(loaded);
        Assert.Equal("alpha", loaded[0].Username);
        Assert.Null(loaded[0].Proxy);
    }

    [Fact]
    public async Task B_saving_any_file_produces_v2_envelope()
    {
        using var tmp = new TempDir();
        var store = BuildStore(tmp.Path, "pw");
        await store.SaveAllAsync(new List<Account>
        {
            new() { UserId = 1, Username = "u", Cookie = "c" },
        });

        // Read raw bytes back, decrypt manually, parse JSON, check schema_version.
        var bytes = await File.ReadAllBytesAsync(Path.Combine(tmp.Path, "AccountData.json"));
        var cipher = new SodiumArgon2idCipher();
        var json = cipher.Decrypt(bytes, "pw");
        using var doc = JsonDocument.Parse(json);
        var version = doc.RootElement.GetProperty("schemaVersion").GetInt32();

        Assert.Equal(AccountEnvelope.CurrentSchemaVersion, version);
        Assert.Equal(2, version);
    }

    [Fact]
    public async Task C_v1_load_save_reload_upgrades_envelope_to_v2_and_preserves_null_Proxy()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "AccountData.json");
        await File.WriteAllBytesAsync(path, EncryptV1Envelope("pw"));

        var store = BuildStore(tmp.Path, "pw");
        var loaded = await store.LoadAllAsync();
        Assert.Null(loaded[0].Proxy);

        // Save → file is now v2
        await store.SaveAllAsync(loaded);
        var bytes = await File.ReadAllBytesAsync(path);
        var json = new SodiumArgon2idCipher().Decrypt(bytes, "pw");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("schemaVersion").GetInt32());

        // Reload and confirm Proxy still null
        var reloaded = await store.LoadAllAsync();
        Assert.Single(reloaded);
        Assert.Null(reloaded[0].Proxy);
        Assert.Equal("alpha", reloaded[0].Username);
        Assert.Equal("C1", reloaded[0].Cookie);
    }

    [Fact]
    public async Task D_v2_file_with_populated_Proxy_round_trips_correctly()
    {
        using var tmp = new TempDir();
        var store = BuildStore(tmp.Path, "pw");

        var original = new Account
        {
            UserId = 42,
            Username = "proxied",
            Cookie = "PC",
            Group = "Bots",
            Proxy = new ProxyConfig(
                ProxyType.Socks5,
                "us-east-04.prx.local",
                51820,
                Username: "u",
                Password: "secret"),
        };

        await store.SaveAllAsync(new[] { original });

        var reloaded = await store.LoadAllAsync();
        Assert.Single(reloaded);
        Assert.NotNull(reloaded[0].Proxy);
        var proxy = reloaded[0].Proxy!;
        Assert.Equal(ProxyType.Socks5, proxy.Type);
        Assert.Equal("us-east-04.prx.local", proxy.Host);
        Assert.Equal(51820, proxy.Port);
        Assert.Equal("u", proxy.Username);
        Assert.Equal("secret", proxy.Password);
    }

    [Fact]
    public async Task E_mixed_v2_load_with_some_proxied_some_not()
    {
        using var tmp = new TempDir();
        var store = BuildStore(tmp.Path, "pw");

        await store.SaveAllAsync(new[]
        {
            new Account { UserId = 1, Username = "a", Cookie = "C1" },
            new Account
            {
                UserId = 2, Username = "b", Cookie = "C2",
                Proxy = new ProxyConfig(ProxyType.Http, "h", 8080),
            },
            new Account { UserId = 3, Username = "c", Cookie = "C3" },
        });

        var reloaded = await store.LoadAllAsync();
        Assert.Equal(3, reloaded.Count);
        Assert.Null(reloaded[0].Proxy);
        Assert.NotNull(reloaded[1].Proxy);
        Assert.Equal(ProxyType.Http, reloaded[1].Proxy!.Type);
        Assert.Null(reloaded[2].Proxy);
    }

    [Fact]
    public async Task Reading_v1_then_save_immediately_produces_v2_without_changes()
    {
        // Explicit name for the "user opens app on stale v1 file → next auto-save
        // upgrades to v2 without any user-visible mutation" scenario. Same shape
        // as test C but logged separately for clarity.
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "AccountData.json");
        await File.WriteAllBytesAsync(path, EncryptV1Envelope("pw"));

        var store = BuildStore(tmp.Path, "pw");
        var loaded = await store.LoadAllAsync();
        await store.SaveAllAsync(loaded); // no mutation between load and save

        var bytes = await File.ReadAllBytesAsync(path);
        var json = new SodiumArgon2idCipher().Decrypt(bytes, "pw");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("accounts").GetArrayLength());
        Assert.Equal("alpha", doc.RootElement.GetProperty("accounts")[0]
                                              .GetProperty("username").GetString());
    }

    [Fact]
    public async Task F_v3_file_in_future_throws_clean_error()
    {
        using var tmp = new TempDir();
        var future = """
            { "schemaVersion": 99, "kdfVariant": "argon2id", "accounts": [], "recentGames": [] }
            """u8.ToArray();

        var cipher = new SodiumArgon2idCipher();
        var encrypted = cipher.Encrypt(future, "pw");
        await File.WriteAllBytesAsync(Path.Combine(tmp.Path, "AccountData.json"), encrypted);

        var store = BuildStore(tmp.Path, "pw");
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAllAsync());
        Assert.Contains("schema_version=99", ex.Message);
        Assert.Contains("max 2", ex.Message);
    }
}
