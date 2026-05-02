using RAM.Core.Models;

namespace RAM.Storage.Json;

/// <summary>
/// JSON wrapper for the account file payload. Persisted to disk after encryption.
///
/// <para>Schema versions:
/// <list type="bullet">
///   <item><b>1</b> — initial. No <see cref="Account.Proxy"/> field on accounts.</item>
///   <item><b>2</b> — adds <see cref="Account.Proxy"/> (nullable). Reader is tolerant
///         of v1 files (missing field → null); writer always emits v2.</item>
/// </list>
/// </para>
/// </summary>
public sealed record AccountEnvelope
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string KdfVariant { get; init; } = "argon2id";
    public IReadOnlyList<Account> Accounts { get; init; } = Array.Empty<Account>();
    public IReadOnlyList<RecentGame> RecentGames { get; init; } = Array.Empty<RecentGame>();
}
