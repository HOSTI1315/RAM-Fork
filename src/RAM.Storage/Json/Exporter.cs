using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAM.Core.Models;

namespace RAM.Storage.Json;

/// <summary>
/// Exports accounts to a plain (unencrypted) JSON file. The output envelope opens with
/// a <c>_warning</c> field flagging that cookies are stored in cleartext — JSON has no
/// comment syntax, so the warning is a regular field that re-imports without harm.
///
/// <para>Proxy passwords are stripped from the export — username + host/port stay so
/// re-import is mostly seamless, but the user must re-enter the password via the
/// detail panel afterwards.</para>
/// </summary>
public sealed class Exporter
{
    public const string WarningText =
        "Plaintext JSON export. Cookies are in cleartext — protect this file. " +
        "Proxy passwords have been excluded from this export — re-enter them after import.";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILogger<Exporter> _logger;

    public Exporter(ILogger<Exporter> logger) => _logger = logger;

    public sealed record ExportEnvelope(
        string _warning,
        DateTimeOffset ExportedAt,
        int SchemaVersion,
        IReadOnlyList<Account> Accounts);

    public async Task SaveAsync(IReadOnlyList<Account> accounts, string filePath, CancellationToken ct = default)
    {
        // Strip Proxy.Password before serialize. Username stays (it's a label, not a
        // credential by itself — typical proxy "username" is an account/route hint).
        // Re-import will leave Password as null and the user must re-enter it via
        // the detail panel.
        var sanitized = accounts.Select(a =>
            a.Proxy is null
                ? a
                : a with { Proxy = a.Proxy with { Password = null } }
        ).ToList();

        var envelope = new ExportEnvelope(
            _warning: WarningText,
            ExportedAt: DateTimeOffset.UtcNow,
            SchemaVersion: 2,
            Accounts: sanitized);

        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, Options);

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(filePath, json, ct);

        _logger.LogInformation("Exported {Count} accounts to {Path}", accounts.Count, filePath);
    }
}
