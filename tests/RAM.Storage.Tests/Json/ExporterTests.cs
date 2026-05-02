using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RAM.Core.Models;
using RAM.Storage.Crypto;
using RAM.Storage.Json;

namespace RAM.Storage.Tests.Json;

public class ExporterTests
{
    [Fact]
    public async Task Exports_envelope_with_warning_field()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ram-exp-{Guid.NewGuid():N}.json");
        try
        {
            var exporter = new Exporter(NullLogger<Exporter>.Instance);
            await exporter.SaveAsync(
                new[]
                {
                    new Account { UserId = 1, Username = "u", Cookie = "c1" },
                },
                path);

            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("Plaintext JSON export", text);
            Assert.Contains("\"_warning\"", text);
            Assert.Contains("\"exportedAt\"", text);
            Assert.Contains("\"u\"", text);

            // Valid JSON parse
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("_warning", out _));
            Assert.True(root.TryGetProperty("accounts", out var arr));
            Assert.Equal(1, arr.GetArrayLength());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Round_trip_Importer_Exporter_Importer_preserves_data()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ram-rt-{Guid.NewGuid():N}.json");
        try
        {
            var original = new[]
            {
                new Account { UserId = 1, Username = "alpha", Cookie = "C1",
                              Group = "01 Farming", Alias = "main",
                              Description = "primary", Tags = new[] { "vip" },
                              BrowserTrackerId = "BT1",
                              Fields = new Dictionary<string, string> { ["k"] = "v" } },
                new Account { UserId = 2, Username = "beta", Cookie = "C2", Group = "02 Trading" },
            };

            var exporter = new Exporter(NullLogger<Exporter>.Instance);
            await exporter.SaveAsync(original, path);

            var importer = new Importer(new DpapiFallback(), NullLogger<Importer>.Instance);
            var imported = await importer.ImportAsync(path);

            Assert.Equal(Importer.ImportSourceFormat.ModernEnvelope, imported.Format);
            Assert.Equal(2, imported.Accounts.Count);
            Assert.Equal(original[0].Cookie, imported.Accounts[0].Cookie);
            Assert.Equal(original[0].Group, imported.Accounts[0].Group);
            Assert.Equal(original[0].Alias, imported.Accounts[0].Alias);
            Assert.Equal(original[0].Tags, imported.Accounts[0].Tags);
            Assert.Equal(original[0].Fields["k"], imported.Accounts[0].Fields["k"]);
            Assert.Equal(original[0].BrowserTrackerId, imported.Accounts[0].BrowserTrackerId);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Round_trip_strips_proxy_password_keeps_other_fields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ram-rt-proxy-{Guid.NewGuid():N}.json");
        try
        {
            var original = new[]
            {
                new Account
                {
                    UserId = 1, Username = "alpha", Cookie = "C1",
                    Proxy = new ProxyConfig(
                        ProxyType.Socks5, "host.local", 1080,
                        Username: "alice",
                        Password: "very-secret"),
                },
                new Account { UserId = 2, Username = "beta", Cookie = "C2" }, // no proxy
            };

            var exporter = new Exporter(NullLogger<Exporter>.Instance);
            await exporter.SaveAsync(original, path);

            // Plain text inspection: password should NOT appear in the file.
            var text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("very-secret", text);
            Assert.Contains("alice", text);                      // username preserved
            Assert.Contains("excluded from this export", text);  // updated warning

            var importer = new Importer(new DpapiFallback(), NullLogger<Importer>.Instance);
            var imported = await importer.ImportAsync(path);

            Assert.Equal(2, imported.Accounts.Count);
            Assert.NotNull(imported.Accounts[0].Proxy);
            Assert.Equal(ProxyType.Socks5, imported.Accounts[0].Proxy!.Type);
            Assert.Equal("host.local", imported.Accounts[0].Proxy.Host);
            Assert.Equal(1080, imported.Accounts[0].Proxy.Port);
            Assert.Equal("alice", imported.Accounts[0].Proxy.Username);
            Assert.Null(imported.Accounts[0].Proxy.Password);    // stripped on export

            Assert.Null(imported.Accounts[1].Proxy);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Warning_text_mentions_both_cleartext_cookies_and_proxy_passwords()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ram-warn-{Guid.NewGuid():N}.json");
        try
        {
            var exporter = new Exporter(NullLogger<Exporter>.Instance);
            await exporter.SaveAsync(Array.Empty<Account>(), path);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("cleartext", text);
            Assert.Contains("Proxy passwords", text);
            Assert.Contains("re-enter", text);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Save_creates_directory_if_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ram-exp-dir-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "nested", "out.json");
        try
        {
            var exporter = new Exporter(NullLogger<Exporter>.Instance);
            await exporter.SaveAsync(Array.Empty<Account>(), path);
            Assert.True(File.Exists(path));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }
}
