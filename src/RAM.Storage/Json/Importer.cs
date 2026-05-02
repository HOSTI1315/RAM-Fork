using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAM.Core.Models;
using RAM.Storage.Crypto;

namespace RAM.Storage.Json;

/// <summary>
/// Imports accounts from external JSON files. Supports three shapes:
/// <list type="number">
///   <item>Modern <c>AccountEnvelope</c> (our format) — plain or DPAPI-encrypted.</item>
///   <item>Legacy RAM <c>AccountData.json</c> — array of objects with fields like
///         <c>SecurityToken</c> (cookie), <c>UserID</c>, <c>Username</c>, <c>Group</c>,
///         <c>Alias</c>, <c>Description</c>, <c>Fields</c>, <c>BrowserTrackerID</c>.</item>
///   <item>Plain JSON array of bare account-like objects (best-effort).</item>
/// </list>
/// Argon2i+SecretBox-encrypted RAM files require the master password and are out of scope
/// for the file-importer; users with those should migrate via <see cref="AccountStore"/>'s
/// in-place migration chain (drop file in data dir, load).
/// </summary>
public sealed class Importer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly DpapiFallback _dpapi;
    private readonly ILogger<Importer> _logger;

    public Importer(DpapiFallback dpapi, ILogger<Importer> logger)
    {
        _dpapi = dpapi;
        _logger = logger;
    }

    public sealed record ImportResult(
        IReadOnlyList<Account> Accounts,
        IReadOnlyList<string> Warnings,
        ImportSourceFormat Format);

    public enum ImportSourceFormat { ModernEnvelope, LegacyRamArray, Unknown }

    public Task<ImportResult> ImportAsync(string filePath, CancellationToken ct = default)
        => ImportFromBytesAsync(File.ReadAllBytes(filePath), ct);

    public Task<ImportResult> ImportFromBytesAsync(byte[] bytes, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        var json = TryDecode(bytes, warnings);
        if (json is null)
            throw new InvalidDataException(
                "Could not decode the file. Tried plain JSON and DPAPI-decrypt; both failed. " +
                "If the source is RAM with Argon2i+SecretBox encryption, drop the file directly into " +
                "%LocalAppData%\\RAM\\AccountData.json and let the in-place migration run.");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accounts = new List<Account>();
        var format = ImportSourceFormat.Unknown;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("accounts", out var accountsProp))
        {
            // Modern envelope shape
            format = ImportSourceFormat.ModernEnvelope;
            foreach (var elem in accountsProp.EnumerateArray())
            {
                var a = ParseModern(elem, warnings);
                if (a is not null) accounts.Add(a);
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            // Legacy RAM array shape
            format = ImportSourceFormat.LegacyRamArray;
            foreach (var elem in root.EnumerateArray())
            {
                var a = ParseLegacyRam(elem, warnings);
                if (a is not null) accounts.Add(a);
            }
        }
        else
        {
            warnings.Add("Unrecognized JSON root shape — expected object with 'accounts' or array.");
        }

        _logger.LogInformation(
            "Imported {Count} accounts from {Format}, {WarningCount} warning(s)",
            accounts.Count, format, warnings.Count);

        return Task.FromResult(new ImportResult(accounts, warnings, format));
    }

    /// <summary>
    /// Reads bytes and tries to decode them into a JSON UTF-8 string. Tries (in order):
    /// raw UTF-8 → DPAPI-decrypt → RAM2 envelope (header magic check, but we can't
    /// decrypt without the password — surfaced as a warning).
    /// </summary>
    private byte[]? TryDecode(byte[] bytes, List<string> warnings)
    {
        // Plain UTF-8 JSON
        if (LooksLikeJson(bytes)) return bytes;

        // DPAPI-encrypted (legacy RAM)
        var dpapi = _dpapi.TryUnprotect(bytes);
        if (dpapi is not null && LooksLikeJson(dpapi))
        {
            warnings.Add("Source was DPAPI-encrypted; decrypted under current Windows user.");
            return dpapi;
        }

        // RAM2 envelope (Argon2id/Argon2i + SecretBox)
        if (CryptoEnvelope.LooksLikeRam2(bytes))
        {
            warnings.Add(
                "Source is RAM2-format (Argon2 + SecretBox) and requires the master password " +
                "to decrypt. Use the in-place migration via AccountStore instead of import.");
            return null;
        }

        warnings.Add("Source is neither plain JSON nor a recognized RAM legacy format.");
        return null;
    }

    private static bool LooksLikeJson(byte[] bytes)
    {
        if (bytes.Length < 2) return false;
        // Skip leading whitespace / BOM
        var i = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            i = 3;
        while (i < bytes.Length && (bytes[i] == ' ' || bytes[i] == '\t' || bytes[i] == '\r' || bytes[i] == '\n'))
            i++;
        return i < bytes.Length && (bytes[i] == '[' || bytes[i] == '{');
    }

    private static Account? ParseModern(JsonElement elem, List<string> warnings)
    {
        try
        {
            var a = elem.Deserialize<Account>(Options);
            return a;
        }
        catch (JsonException ex)
        {
            warnings.Add($"Skipped account (modern parse failed): {ex.Message}");
            return null;
        }
    }

    private static Account? ParseLegacyRam(JsonElement elem, List<string> warnings)
    {
        // Tolerant lookup — case-insensitive, skips missing fields.
        string? cookie = TryGetString(elem, "SecurityToken") ?? TryGetString(elem, "cookie");
        string? username = TryGetString(elem, "Username");
        ulong userId = TryGetUlong(elem, "UserID") ?? TryGetUlong(elem, "userId") ?? 0;

        if (string.IsNullOrWhiteSpace(cookie) || string.IsNullOrWhiteSpace(username))
        {
            warnings.Add($"Skipped legacy entry: missing SecurityToken or Username (UserID={userId}).");
            return null;
        }

        return new Account
        {
            UserId = userId,
            Username = username!,
            Cookie = cookie!,
            DisplayName = TryGetString(elem, "DisplayName") ?? string.Empty,
            Group = TryGetString(elem, "Group") ?? string.Empty,
            Alias = TryGetString(elem, "Alias") ?? TryGetString(elem, "_Alias") ?? string.Empty,
            Description = TryGetString(elem, "Description") ?? TryGetString(elem, "_Description") ?? string.Empty,
            BrowserTrackerId = TryGetString(elem, "BrowserTrackerID") ?? string.Empty,
            Fields = TryGetDict(elem, "Fields"),
            LastUsed = TryGetDate(elem, "LastUse"),
        };
    }

    private static string? TryGetString(JsonElement elem, string name)
    {
        if (!elem.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Null) return null;
        if (p.ValueKind == JsonValueKind.String) return p.GetString();
        return p.ToString();
    }

    private static ulong? TryGetUlong(JsonElement elem, string name)
    {
        if (!elem.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetUInt64(out var u) => u,
            JsonValueKind.Number when p.TryGetInt64(out var i) && i >= 0 => (ulong)i,
            JsonValueKind.String when ulong.TryParse(p.GetString(), out var s) => s,
            _ => null,
        };
    }

    private static DateTimeOffset? TryGetDate(JsonElement elem, string name)
    {
        if (!elem.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(p.GetString(), out var dt)) return dt;
        return null;
    }

    private static IReadOnlyDictionary<string, string> TryGetDict(JsonElement elem, string name)
    {
        if (!elem.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, string>();
        var dict = new Dictionary<string, string>();
        foreach (var prop in p.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
                dict[prop.Name] = prop.Value.GetString() ?? string.Empty;
        }
        return dict;
    }
}
