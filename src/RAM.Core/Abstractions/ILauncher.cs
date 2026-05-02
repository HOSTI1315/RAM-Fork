using RAM.Core.Models;

namespace RAM.Core.Abstractions;

public interface ILauncher
{
    Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default);
}

public sealed record LaunchRequest(
    Account Account,
    LaunchTarget Target,
    BotProfile Profile = BotProfile.Normal);

public abstract record LaunchTarget
{
    /// <summary>Standard place launch. JobId optional — if set, joins specific server.</summary>
    public sealed record Place(ulong PlaceId, string? JobId = null) : LaunchTarget;

    /// <summary>Joins the place where the target user is currently in.</summary>
    public sealed record FollowUser(ulong UserId) : LaunchTarget;

    /// <summary>Private/VIP server. LinkCode is the share-link code.</summary>
    public sealed record PrivateServer(ulong PlaceId, string LinkCode) : LaunchTarget;
}

public sealed record LaunchResult
{
    public required bool IsSuccess { get; init; }
    public int? ProcessId { get; init; }
    public string? BrowserTrackerId { get; init; }
    public string? Error { get; init; }

    public static LaunchResult Ok(int pid, string trackerId) =>
        new() { IsSuccess = true, ProcessId = pid, BrowserTrackerId = trackerId };

    public static LaunchResult Fail(string error) =>
        new() { IsSuccess = false, Error = error };
}
