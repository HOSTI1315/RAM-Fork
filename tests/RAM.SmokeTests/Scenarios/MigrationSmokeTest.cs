using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Storage;
using RAM.Storage.Crypto;

namespace RAM.SmokeTests.Scenarios;

internal static class MigrationSmokeTest
{
    public static async Task<string?> RunAsync()
    {
        using var temp = new TempRoot("migration");
        var path = Path.Combine(temp.Path, "AccountData.json");

        // Stage a DPAPI-encrypted legacy account file
        var legacyPayload = """
            {
              "schemaVersion": 1,
              "kdfVariant": "dpapi-legacy",
              "accounts": [
                { "userId": 99, "username": "legacy_alpha", "cookie": "L_COOKIE_A",
                  "group": "Imported", "alias": "", "description": "", "tags": [],
                  "browserTrackerId": "", "fields": {}, "disabled": false,
                  "created": "2024-01-01T00:00:00+00:00" },
                { "userId": 100, "username": "legacy_beta", "cookie": "L_COOKIE_B",
                  "group": "Imported", "alias": "", "description": "", "tags": [],
                  "browserTrackerId": "", "fields": {}, "disabled": false,
                  "created": "2024-01-01T00:00:00+00:00" }
              ],
              "recentGames": []
            }
            """u8.ToArray();

        var dpapi = new DpapiFallback();
        var protectedBytes = dpapi.Protect(legacyPayload);
        await File.WriteAllBytesAsync(path, protectedBytes);

        var beforeBytes = await File.ReadAllBytesAsync(path);
        var beforeIsRam2 = CryptoEnvelope.LooksLikeRam2(beforeBytes);
        if (beforeIsRam2)
            throw new InvalidOperationException("Pre-condition failed: file already in RAM2 format");

        // Build a DI scope rooted at our temp dir with a fixed password
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRamStorage(rootDirectory: temp.Path, passwordProvider: () => "smoke-pw");
        await using var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<IAccountStore>();
        var loaded = await store.LoadAllAsync();
        if (loaded.Count != 2)
            throw new InvalidOperationException($"Expected 2 accounts, loaded {loaded.Count}");
        if (loaded[0].Username != "legacy_alpha" || loaded[0].Cookie != "L_COOKIE_A")
            throw new InvalidOperationException("Account fields not migrated correctly");

        // Save to trigger re-encryption under Argon2id
        await store.SaveAllAsync(loaded);

        var afterBytes = await File.ReadAllBytesAsync(path);
        if (!CryptoEnvelope.LooksLikeRam2(afterBytes))
            throw new InvalidOperationException("Post-save file is not in RAM2 envelope format");
        var layout = CryptoEnvelope.Unpack(afterBytes);
        if (layout.Kdf != KdfVariant.Argon2id)
            throw new InvalidOperationException($"Expected Argon2id, got {layout.Kdf}");

        // Re-load to confirm Argon2id round-trip works
        var reloaded = await store.LoadAllAsync();
        if (reloaded.Count != 2 || reloaded.Any(a => string.IsNullOrEmpty(a.Cookie)))
            throw new InvalidOperationException("Argon2id reload lost account data");

        return $"DPAPI→Argon2id migration OK: {loaded.Count} accounts, " +
               $"envelope KDF = {layout.Kdf}, ops={layout.OpsLimit}, mem={layout.MemKb}KB";
    }
}
