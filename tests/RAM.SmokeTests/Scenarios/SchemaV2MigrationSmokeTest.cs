using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAM.Core.Abstractions;
using RAM.Storage;
using RAM.Storage.Crypto;
using RAM.Storage.Json;

namespace RAM.SmokeTests.Scenarios;

/// <summary>
/// End-to-end migration smoke for schema_version 1 → 2 (Proxy field addition).
/// Generates a v1 fixture inline (no repo file), runs it through the production
/// AccountStore via DI, and verifies the next save produces a v2 envelope.
/// </summary>
internal static class SchemaV2MigrationSmokeTest
{
    public static async Task<string?> RunAsync()
    {
        using var temp = new TempRoot("schema-v2");

        // 1. Build a v1 envelope manually (no `proxy` field anywhere)
        var v1Json = """
            {
              "schemaVersion": 1,
              "kdfVariant": "argon2id",
              "accounts": [
                { "userId": 101, "username": "smoke_alpha", "displayName": "",
                  "cookie": "SMOKE_C1", "group": "Imported", "alias": "",
                  "description": "", "tags": [], "browserTrackerId": "",
                  "fields": {}, "windowPlacement": null,
                  "pinHash": null, "pinUnlockedUntil": null,
                  "disabled": false,
                  "created": "2025-01-01T00:00:00+00:00", "lastUsed": null },
                { "userId": 102, "username": "smoke_beta", "displayName": "",
                  "cookie": "SMOKE_C2", "group": "Imported", "alias": "",
                  "description": "", "tags": [], "browserTrackerId": "",
                  "fields": {}, "windowPlacement": null,
                  "pinHash": null, "pinUnlockedUntil": null,
                  "disabled": false,
                  "created": "2025-01-01T00:00:00+00:00", "lastUsed": null }
              ],
              "recentGames": []
            }
            """u8.ToArray();

        const string smokePassword = "schema-smoke-pw";

        // 2. Encrypt with the production cipher
        var cipher = new SodiumArgon2idCipher();
        var encrypted = cipher.Encrypt(v1Json, smokePassword);
        var path = Path.Combine(temp.Path, "AccountData.json");
        await File.WriteAllBytesAsync(path, encrypted);

        // 3. Load via DI host (production wiring)
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddRamStorage(rootDirectory: temp.Path, passwordProvider: () => smokePassword);
        await using var sp = builder.Services.BuildServiceProvider();

        var store = sp.GetRequiredService<IAccountStore>();
        var loaded = await store.LoadAllAsync();

        if (loaded.Count != 2)
            throw new InvalidOperationException($"Expected 2 accounts, got {loaded.Count}");

        // 4. Assert all Proxy=null after v1 → migrated load
        foreach (var a in loaded)
            if (a.Proxy is not null)
                throw new InvalidOperationException(
                    $"Account {a.Username} should have null Proxy after v1 load, got {a.Proxy}");

        // 5. SaveAllAsync — should produce a v2 envelope
        await store.SaveAllAsync(loaded);

        // 6. Read raw bytes back, decrypt, parse JSON, assert schemaVersion == 2
        var afterBytes = await File.ReadAllBytesAsync(path);
        var afterJson = cipher.Decrypt(afterBytes, smokePassword);
        using var doc = JsonDocument.Parse(afterJson);
        var schemaVersion = doc.RootElement.GetProperty("schemaVersion").GetInt32();

        if (schemaVersion != 2)
            throw new InvalidOperationException(
                $"Expected schema_version=2 after save, got {schemaVersion}");

        // 7. Spot-check that account data is intact (no truncation / mutation)
        var accountsArr = doc.RootElement.GetProperty("accounts");
        if (accountsArr.GetArrayLength() != 2)
            throw new InvalidOperationException(
                $"Expected 2 accounts in saved file, got {accountsArr.GetArrayLength()}");
        var firstUsername = accountsArr[0].GetProperty("username").GetString();
        if (firstUsername != "smoke_alpha")
            throw new InvalidOperationException(
                $"First account username = '{firstUsername}', expected 'smoke_alpha'");

        return $"v1→v2 migration: 2 accounts loaded with Proxy=null, " +
               $"saved as schemaVersion={schemaVersion}, " +
               $"data intact ({accountsArr.GetArrayLength()} accounts in v2 file)";
    }
}
