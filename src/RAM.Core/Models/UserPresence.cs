namespace RAM.Core.Models;

public sealed record UserPresence
{
    public required ulong UserId { get; init; }
    public PresenceType Type { get; init; }
    public ulong? PlaceId { get; init; }
    public ulong? RootPlaceId { get; init; }
    public ulong? UniverseId { get; init; }
    public string? GameId { get; init; }
    public DateTimeOffset? LastOnline { get; init; }
    public string? LastLocation { get; init; }
}
