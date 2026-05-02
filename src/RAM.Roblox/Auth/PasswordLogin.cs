using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAM.Roblox.Api;
using RAM.Roblox.Auth.Models;

namespace RAM.Roblox.Auth;

/// <summary>
/// Username/password login with 2FA support (email + authenticator). Two-call flow:
/// <list type="number">
///   <item><see cref="StartAsync"/> sends credentials. Result is either Success (with cookie),
///         TwoFactorRequired (needs <see cref="CompleteTwoFactorAsync"/>), or Failed.</item>
///   <item><see cref="CompleteTwoFactorAsync"/> verifies a 2FA code against the previously
///         issued challenge and completes login.</item>
/// </list>
///
/// Endpoints follow the Roblox v2 login flow as documented in RAM/fork-4. Live behavior
/// drifts; treat this as a starting point and verify against current Roblox responses.
/// </summary>
public sealed class PasswordLogin
{
    private readonly HttpClient _http;
    private readonly RobloxApiOptions _options;
    private readonly CsrfTokenCache _csrf;
    private readonly ILogger<PasswordLogin> _logger;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PasswordLogin(
        HttpClient http,
        IOptions<RobloxApiOptions> options,
        CsrfTokenCache csrf,
        ILogger<PasswordLogin> logger)
    {
        _http = http;
        _options = options.Value;
        _csrf = csrf;
        _logger = logger;
    }

    public async Task<LoginResult> StartAsync(string username, string password, CancellationToken ct = default)
    {
        var url = $"{_options.AuthBaseUrl}/v2/login";
        var body = new V2LoginRequest("Username", username, password);
        using var res = await SendWithCsrfAsync(HttpMethod.Post, url, body, cookie: null, ct);

        // Success path: 200 OK + Set-Cookie .ROBLOSECURITY
        if (res.IsSuccessStatusCode)
        {
            var cookie = ExtractRobloSecurity(res);
            if (cookie is null)
                return new LoginResult.Failed("Login OK but no .ROBLOSECURITY cookie in response");
            var user = await res.Content.ReadFromJsonAsync<V2LoginResponse>(Json, ct);
            if (user?.User is null)
                return new LoginResult.Failed("Login OK but no user payload");
            return new LoginResult.Success(cookie, user.User.Id, user.User.Name, user.User.DisplayName);
        }

        // 2FA path: typically 403 with twoStepVerificationData payload
        if (res.StatusCode == HttpStatusCode.Forbidden)
        {
            try
            {
                var dto = await res.Content.ReadFromJsonAsync<V2LoginResponse>(Json, ct);
                if (dto?.TwoStepVerificationData is { } tsv && dto.User is not null)
                {
                    return new LoginResult.TwoFactorRequired(
                        dto.User.Id,
                        tsv.Ticket,
                        ParseMediaType(tsv.MediaType));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse 2FA challenge");
            }
        }

        return new LoginResult.Failed(
            $"Login rejected: {(int)res.StatusCode} {res.ReasonPhrase}",
            (int)res.StatusCode);
    }

    public async Task<LoginResult> CompleteTwoFactorAsync(
        ulong userId,
        TwoFactorMediaType mediaType,
        string challengeId,
        string code,
        CancellationToken ct = default)
    {
        var verifyPath = mediaType switch
        {
            TwoFactorMediaType.Email => "email-code",
            TwoFactorMediaType.Authenticator => "authenticator",
            TwoFactorMediaType.SecurityKey => "security-key",
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType)),
        };
        var verifyUrl = $"https://twostepverification.roblox.com/v1/users/{userId}/challenges/{verifyPath}/verify";
        var verifyBody = new TwoStepVerifyRequest(challengeId, "Login", code);

        using var verifyRes = await SendWithCsrfAsync(HttpMethod.Post, verifyUrl, verifyBody, cookie: null, ct);
        if (!verifyRes.IsSuccessStatusCode)
            return new LoginResult.Failed(
                $"2FA verification failed: {(int)verifyRes.StatusCode}",
                (int)verifyRes.StatusCode);

        var verification = await verifyRes.Content.ReadFromJsonAsync<TwoStepVerifyResponse>(Json, ct);
        if (verification is null || string.IsNullOrEmpty(verification.VerificationToken))
            return new LoginResult.Failed("2FA verification returned no token");

        // Re-submit login with verificationToken in header
        var url = $"{_options.AuthBaseUrl}/v2/login";
        var loginBody = new V2LoginRequest("AuthToken", challengeId, verification.VerificationToken);
        using var loginRes = await SendWithCsrfAsync(
            HttpMethod.Post, url, loginBody, cookie: null, ct,
            configure: req => req.Headers.TryAddWithoutValidation(
                "rblx-challenge-metadata", verification.VerificationToken));

        if (!loginRes.IsSuccessStatusCode)
            return new LoginResult.Failed(
                $"Final login failed: {(int)loginRes.StatusCode}",
                (int)loginRes.StatusCode);

        var cookie = ExtractRobloSecurity(loginRes);
        if (cookie is null)
            return new LoginResult.Failed("Final login OK but no .ROBLOSECURITY cookie");
        var user = await loginRes.Content.ReadFromJsonAsync<V2LoginResponse>(Json, ct);
        if (user?.User is null)
            return new LoginResult.Failed("Final login OK but no user payload");
        return new LoginResult.Success(cookie, user.User.Id, user.User.Name, user.User.DisplayName);
    }

    private static TwoFactorMediaType ParseMediaType(string raw) => raw.ToLowerInvariant() switch
    {
        "email" => TwoFactorMediaType.Email,
        "authenticator" => TwoFactorMediaType.Authenticator,
        "securitykey" or "security-key" => TwoFactorMediaType.SecurityKey,
        _ => TwoFactorMediaType.Email,
    };

    private static string? ExtractRobloSecurity(HttpResponseMessage res)
    {
        if (!res.Headers.TryGetValues("Set-Cookie", out var setCookies)) return null;
        foreach (var line in setCookies)
        {
            const string prefix = ".ROBLOSECURITY=";
            var i = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (i < 0) continue;
            var start = i + prefix.Length;
            var end = line.IndexOf(';', start);
            return end < 0 ? line[start..] : line[start..end];
        }
        return null;
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync<T>(
        HttpMethod method,
        string url,
        T body,
        string? cookie,
        CancellationToken ct,
        Action<HttpRequestMessage>? configure = null)
    {
        async Task<HttpResponseMessage> SendOnce(string? token)
        {
            var req = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: Json) };
            if (cookie is not null) req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
            if (token is not null) req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
            configure?.Invoke(req);
            return await _http.SendAsync(req, ct);
        }

        var key = cookie ?? "anonymous";
        var cached = _csrf.Get(key);
        var res = await SendOnce(cached);
        if (res.StatusCode == HttpStatusCode.Forbidden &&
            res.Headers.TryGetValues("x-csrf-token", out var fresh))
        {
            res.Dispose();
            var newToken = fresh.First();
            _csrf.Set(key, newToken);
            res = await SendOnce(newToken);
        }
        return res;
    }
}
