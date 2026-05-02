using RAM.Core.Models;

namespace RAM.Core.Abstractions;

public interface IRobloxApi
{
    Task<bool> ValidateCookieAsync(string cookie, CancellationToken ct = default);

    Task<AuthenticatedUser?> GetAuthenticatedUserAsync(string cookie, CancellationToken ct = default);

    Task<string> GetCsrfTokenAsync(string cookie, CancellationToken ct = default);

    /// <summary>Retrieves an rbx-authentication-ticket for client launch.</summary>
    Task<string> GetAuthTicketAsync(string cookie, CancellationToken ct = default);

    /// <summary>Batch presence for up to ~100 users in a single call.</summary>
    Task<IReadOnlyDictionary<ulong, UserPresence>> GetPresenceAsync(
        IReadOnlyCollection<ulong> userIds,
        string cookie,
        CancellationToken ct = default);

    /// <summary>Batch thumbnails (avatar headshot, place icon, etc.).</summary>
    Task<IReadOnlyDictionary<ThumbnailKey, ThumbnailResult>> GetThumbnailsAsync(
        IReadOnlyCollection<ThumbnailKey> requests,
        CancellationToken ct = default);

    Task<UniverseInfo?> GetUniverseFromPlaceAsync(ulong placeId, CancellationToken ct = default);

    Task<long> GetRobuxAsync(string cookie, CancellationToken ct = default);
}

public sealed record AuthenticatedUser(ulong Id, string Name, string DisplayName);

public sealed record UniverseInfo(ulong UniverseId, ulong RootPlaceId, string Name);

public sealed record ThumbnailKey(ulong TargetId, string Type, string Size, string Format = "Png");

public sealed record ThumbnailResult(string? ImageUrl, string State);
