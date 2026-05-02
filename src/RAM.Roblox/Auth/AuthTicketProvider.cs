using Microsoft.Extensions.Logging;
using RAM.Core.Abstractions;

namespace RAM.Roblox.Auth;

/// <summary>
/// Thin facade over <see cref="IRobloxApi.GetAuthTicketAsync"/>. Centralizes the call
/// site so the launcher can swap providers (e.g. for caching or test doubles) without
/// changing call sites.
/// </summary>
public sealed class AuthTicketProvider
{
    private readonly IRobloxApi _api;
    private readonly ILogger<AuthTicketProvider> _logger;

    public AuthTicketProvider(IRobloxApi api, ILogger<AuthTicketProvider> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<string> GetAsync(string cookie, CancellationToken ct = default)
    {
        var ticket = await _api.GetAuthTicketAsync(cookie, ct);
        _logger.LogDebug("Acquired auth ticket (length {Length})", ticket.Length);
        return ticket;
    }
}
