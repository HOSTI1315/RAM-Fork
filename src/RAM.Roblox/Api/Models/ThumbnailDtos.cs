using System.Text.Json.Serialization;

namespace RAM.Roblox.Api.Models;

internal sealed record ThumbnailBatchRequestItem(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("targetId")] ulong TargetId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("size")] string Size,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("isCircular")] bool IsCircular = false);

internal sealed record ThumbnailBatchResponse(
    [property: JsonPropertyName("data")] ThumbnailBatchResponseItem[] Data);

internal sealed record ThumbnailBatchResponseItem(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("targetId")] ulong TargetId,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl);
