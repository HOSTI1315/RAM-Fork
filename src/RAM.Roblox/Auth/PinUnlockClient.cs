using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAM.Roblox.Api;
using RAM.Roblox.Auth.Models;

namespace RAM.Roblox.Auth;

/// <summary>
/// Unlocks an account's transaction PIN for ~5 minutes via the
/// <c>/v1/account/pin/unlock</c> endpoint. RAM stores the unlock window per account so
/// subsequent operations within the window don't re-prompt.
/// </summary>
public sealed class PinUnlockClient
{
    public static readonly TimeSpan UnlockWindow = TimeSpan.FromMinutes(5);

    private readonly HttpClient _http;
    private readonly RobloxApiOptions _options;
    private readonly CsrfTokenCache _csrf;
    private readonly ILogger<PinUnlockClient> _logger;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PinUnlockClient(
        HttpClient http,
        IOptions<RobloxApiOptions> options,
        CsrfTokenCache csrf,
        ILogger<PinUnlockClient> logger)
    {
        _http = http;
        _options = options.Value;
        _csrf = csrf;
        _logger = logger;
    }

    /// <summary>Returns the time at which the unlock expires, or null on failure.</summary>
    public async Task<DateTimeOffset?> UnlockAsync(string cookie, string pin, CancellationToken ct = default)
    {
        if (pin.Length != 4 || !pin.All(char.IsDigit))
            throw new ArgumentException("PIN must be 4 digits", nameof(pin));

        var url = $"{_options.AuthBaseUrl}/v1/account/pin/unlock";

        async Task<HttpResponseMessage> SendOnce(string? token)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new PinUnlockRequest(pin), options: Json),
            };
            req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
            if (token is not null) req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
            return await _http.SendAsync(req, ct);
        }

        var cached = _csrf.Get(cookie);
        using var res = await (cached is null ? SendOnce(null) : SendOnce(cached));
        if (res.StatusCode == HttpStatusCode.Forbidden &&
            res.Headers.TryGetValues("x-csrf-token", out var fresh))
        {
            var newToken = fresh.First();
            _csrf.Set(cookie, newToken);
            using var retry = await SendOnce(newToken);
            return ProcessResult(retry);
        }
        return ProcessResult(res);

        DateTimeOffset? ProcessResult(HttpResponseMessage r)
        {
            if (r.IsSuccessStatusCode)
            {
                _logger.LogDebug("PIN unlocked for ~5 minutes");
                return DateTimeOffset.UtcNow + UnlockWindow;
            }
            _logger.LogWarning("PIN unlock failed: {Status}", (int)r.StatusCode);
            return null;
        }
    }
}
