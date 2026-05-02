using System.Text.Json.Serialization;

namespace RAM.Roblox.Api.Models;

internal sealed record AuthenticatedUserDto(
    [property: JsonPropertyName("id")] ulong Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string DisplayName);

internal sealed record UniverseFromPlaceDto(
    [property: JsonPropertyName("universeId")] ulong UniverseId);

internal sealed record GameDetailsDto(
    [property: JsonPropertyName("data")] GameDetailsItem[] Data);

internal sealed record GameDetailsItem(
    [property: JsonPropertyName("id")] ulong Id,
    [property: JsonPropertyName("rootPlaceId")] ulong RootPlaceId,
    [property: JsonPropertyName("name")] string Name);

internal sealed record RobuxBalanceDto(
    [property: JsonPropertyName("robux")] long Robux);
