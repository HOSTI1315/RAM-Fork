using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Roblox.Api;

/// <summary>
/// Default <see cref="IPresenceProvider"/> impl: forwards to <see cref="IRobloxApi"/>.
/// Caller supplies the cookie via a closure-captured provider since the interface itself
/// is cookie-agnostic (future WebSocket impl will hold its own session state).
/// </summary>
public sealed class PollingPresenceProvider : IPresenceProvider
{
    private readonly IRobloxApi _api;
    private readonly Func<string?> _cookieProvider;

    public PollingPresenceProvider(IRobloxApi api, Func<string?> cookieProvider)
    {
        _api = api;
        _cookieProvider = cookieProvider;
    }

    public async Task<IReadOnlyDictionary<ulong, UserPresence>> GetPresenceAsync(
        IReadOnlyCollection<ulong> userIds, CancellationToken ct = default)
    {
        var cookie = _cookieProvider();
        if (string.IsNullOrEmpty(cookie) || userIds.Count == 0)
            return new Dictionary<ulong, UserPresence>();
        return await _api.GetPresenceAsync(userIds, cookie, ct);
    }
}
