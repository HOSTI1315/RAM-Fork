namespace RAM.Core.Models;

public sealed record Account
{
    public required ulong UserId { get; init; }
    public required string Username { get; init; }
    public string DisplayName { get; init; } = "";

    public required string Cookie { get; init; }

    public string Group { get; init; } = "";
    public string Alias { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public string BrowserTrackerId { get; init; } = "";

    public IReadOnlyDictionary<string, string> Fields { get; init; }
        = new Dictionary<string, string>();

    public WindowPlacement? WindowPlacement { get; init; }

    public string? PinHash { get; init; }
    public DateTimeOffset? PinUnlockedUntil { get; init; }

    public bool Disabled { get; init; }
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsed { get; init; }

    /// <summary>
    /// Optional per-account proxy. <c>null</c> means "use direct connection". Added in
    /// schema_version 2; v1 files deserialize cleanly with this set to null.
    /// </summary>
    public ProxyConfig? Proxy { get; init; }
}
