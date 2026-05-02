using System.Text.Json.Serialization;

namespace RAM.Roblox.Auth.Models;

internal sealed record V2LoginRequest(
    [property: JsonPropertyName("ctype")] string Ctype,
    [property: JsonPropertyName("cvalue")] string Cvalue,
    [property: JsonPropertyName("password")] string Password);

internal sealed record V2LoginResponse(
    [property: JsonPropertyName("user")] V2LoginUser? User,
    [property: JsonPropertyName("twoStepVerificationData")] TwoStepVerificationData? TwoStepVerificationData);

internal sealed record V2LoginUser(
    [property: JsonPropertyName("id")] ulong Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string DisplayName);

internal sealed record TwoStepVerificationData(
    [property: JsonPropertyName("mediaType")] string MediaType,
    [property: JsonPropertyName("ticket")] string Ticket);

internal sealed record TwoStepVerifyRequest(
    [property: JsonPropertyName("challengeId")] string ChallengeId,
    [property: JsonPropertyName("actionType")] string ActionType,
    [property: JsonPropertyName("code")] string Code);

internal sealed record TwoStepVerifyResponse(
    [property: JsonPropertyName("verificationToken")] string VerificationToken);

internal sealed record PinUnlockRequest(
    [property: JsonPropertyName("pin")] string Pin);
