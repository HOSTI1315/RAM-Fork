namespace RAM.Core.Models;

public sealed record RecentGame
{
    public required ulong PlaceId { get; init; }
    public ulong UniverseId { get; init; }
    public required string Name { get; init; }
    public string? Region { get; init; }
    public DateTimeOffset LastJoinedAt { get; init; } = DateTimeOffset.UtcNow;
}
