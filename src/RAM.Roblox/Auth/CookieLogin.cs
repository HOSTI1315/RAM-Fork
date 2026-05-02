using Microsoft.Extensions.Logging;
using RAM.Core.Abstractions;

namespace RAM.Roblox.Auth;

/// <summary>
/// Validates a `.ROBLOSECURITY` cookie and returns the authenticated user. Used by both
/// initial import and recurring revalidation.
/// </summary>
public sealed class CookieLogin
{
    private readonly IRobloxApi _api;
    private readonly ILogger<CookieLogin> _logger;

    public CookieLogin(IRobloxApi api, ILogger<CookieLogin> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<LoginResult> ValidateAsync(string cookie, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cookie))
            return new LoginResult.Failed("Empty cookie");

        try
        {
            var user = await _api.GetAuthenticatedUserAsync(cookie, ct);
            if (user is null)
            {
                _logger.LogDebug("Cookie did not validate (not authenticated)");
                return new LoginResult.Failed("Cookie invalid or expired", 401);
            }
            return new LoginResult.Success(cookie, user.Id, user.Name, user.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cookie validation threw");
            return new LoginResult.Failed($"Cookie validation error: {ex.GetType().Name}");
        }
    }
}
