using System.Text.Json.Serialization;

namespace RAM.Roblox.Api.Models;

internal sealed record PresenceUsersRequest(
    [property: JsonPropertyName("userIds")] ulong[] UserIds);

internal sealed record PresenceUsersResponse(
    [property: JsonPropertyName("userPresences")] PresenceItem[] UserPresences);

internal sealed record PresenceItem(
    [property: JsonPropertyName("userPresenceType")] int UserPresenceType,
    [property: JsonPropertyName("lastLocation")] string? LastLocation,
    [property: JsonPropertyName("placeId")] ulong? PlaceId,
    [property: JsonPropertyName("rootPlaceId")] ulong? RootPlaceId,
    [property: JsonPropertyName("gameId")] string? GameId,
    [property: JsonPropertyName("universeId")] ulong? UniverseId,
    [property: JsonPropertyName("lastOnline")] DateTimeOffset? LastOnline,
    [property: JsonPropertyName("userId")] ulong UserId);
