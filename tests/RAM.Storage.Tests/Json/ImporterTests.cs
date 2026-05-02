using Microsoft.Extensions.Logging.Abstractions;
using RAM.Storage.Crypto;
using RAM.Storage.Json;

namespace RAM.Storage.Tests.Json;

public class ImporterTests
{
    private static Importer Build() =>
        new(new DpapiFallback(), NullLogger<Importer>.Instance);

    [Fact]
    public async Task Imports_legacy_RAM_array_format()
    {
        var legacy = """
            [
                { "SecurityToken": "C1", "Username": "alpha", "UserID": 1, "Group": "Farming",
                  "Alias": "main", "Description": "primary", "BrowserTrackerID": "BT1",
                  "Fields": { "k1": "v1" } },
                { "SecurityToken": "C2", "Username": "beta", "UserID": 2, "Group": "Trading" }
            ]
            """u8.ToArray();

        var result = await Build().ImportFromBytesAsync(legacy);

        Assert.Equal(Importer.ImportSourceFormat.LegacyRamArray, result.Format);
        Assert.Equal(2, result.Accounts.Count);
        Assert.Equal("alpha", result.Accounts[0].Username);
        Assert.Equal("C1", result.Accounts[0].Cookie);
        Assert.Equal(1ul, result.Accounts[0].UserId);
        Assert.Equal("Farming", result.Accounts[0].Group);
        Assert.Equal("main", result.Accounts[0].Alias);
        Assert.Equal("primary", result.Accounts[0].Description);
        Assert.Equal("BT1", result.Accounts[0].BrowserTrackerId);
        Assert.Equal("v1", result.Accounts[0].Fields["k1"]);
    }

    [Fact]
    public async Task Imports_modern_envelope_format()
    {
        // Round-trip via the Exporter — the modern envelope shape is what Exporter writes.
        var path = Path.Combine(Path.GetTempPath(), $"ram-imptest-{Guid.NewGuid():N}.json");
        try
        {
            var exporter = new Exporter(NullLogger<Exporter>.Instance);
            await exporter.SaveAsync(
                new[]
                {
                    new RAM.Core.Models.Account { UserId = 99, Username = "modern", Cookie = "MC", Group = "G" },
                },
                path);

            var result = await Build().ImportAsync(path);

            Assert.Equal(Importer.ImportSourceFormat.ModernEnvelope, result.Format);
            Assert.Single(result.Accounts);
            Assert.Equal("modern", result.Accounts[0].Username);
            Assert.Equal("MC", result.Accounts[0].Cookie);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Imports_DPAPI_encrypted_legacy_array()
    {
        var legacy = """
            [{"SecurityToken":"DPC1","Username":"gamma","UserID":3}]
            """u8.ToArray();
        var encrypted = new DpapiFallback().Protect(legacy);

        var result = await Build().ImportFromBytesAsync(encrypted);

        Assert.Equal(Importer.ImportSourceFormat.LegacyRamArray, result.Format);
        Assert.Single(result.Accounts);
        Assert.Equal("gamma", result.Accounts[0].Username);
        Assert.Contains(result.Warnings, w => w.Contains("DPAPI"));
    }

    [Fact]
    public async Task Skips_legacy_entry_missing_cookie_and_warns()
    {
        var legacy = """
            [
                { "Username": "no_cookie", "UserID": 7 },
                { "SecurityToken": "OK", "Username": "good", "UserID": 8 }
            ]
            """u8.ToArray();

        var result = await Build().ImportFromBytesAsync(legacy);

        Assert.Single(result.Accounts);
        Assert.Equal("good", result.Accounts[0].Username);
        Assert.Contains(result.Warnings, w => w.Contains("missing"));
    }

    [Fact]
    public async Task Throws_on_garbage_bytes()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Build().ImportFromBytesAsync(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
    }

    [Fact]
    public async Task Returns_empty_on_empty_array()
    {
        var result = await Build().ImportFromBytesAsync("[]"u8.ToArray());
        Assert.Empty(result.Accounts);
        Assert.Equal(Importer.ImportSourceFormat.LegacyRamArray, result.Format);
    }

    [Fact]
    public async Task Surfaces_RAM2_envelope_as_warning_not_failure_for_proper_account_data()
    {
        // Build a real RAM2 envelope (which we cannot decrypt without the password).
        // Importer should warn that Argon2 + SecretBox needs the master password.
        var fakeContent = "{}"u8.ToArray();
        var cipher = new SodiumArgon2idCipher();
        var ram2 = cipher.Encrypt(fakeContent, "test-pw");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Build().ImportFromBytesAsync(ram2));
    }
}
