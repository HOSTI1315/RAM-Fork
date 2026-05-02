using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Api.Models;

namespace RAM.Roblox.Api;

public sealed class RobloxApi : IRobloxApi
{
    public const string HttpClientName = "roblox";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly RobloxApiOptions _options;
    private readonly CsrfTokenCache _csrfCache;
    private readonly ILogger<RobloxApi> _logger;

    public RobloxApi(
        HttpClient http,
        IOptions<RobloxApiOptions> options,
        CsrfTokenCache csrfCache,
        ILogger<RobloxApi> logger)
    {
        _http = http;
        _options = options.Value;
        _csrfCache = csrfCache;
        _logger = logger;
    }

    public async Task<bool> ValidateCookieAsync(string cookie, CancellationToken ct = default)
    {
        var user = await GetAuthenticatedUserAsync(cookie, ct);
        return user is not null;
    }

    public async Task<AuthenticatedUser?> GetAuthenticatedUserAsync(string cookie, CancellationToken ct = default)
    {
        using var req = NewRequest(HttpMethod.Get, $"{_options.UsersBaseUrl}/v1/users/authenticated", cookie);
        using var res = await _http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized) return null;
        if (!res.IsSuccessStatusCode) return null;
        var dto = await res.Content.ReadFromJsonAsync<AuthenticatedUserDto>(JsonOptions, ct);
        return dto is null ? null : new AuthenticatedUser(dto.Id, dto.Name, dto.DisplayName);
    }

    public async Task<string> GetCsrfTokenAsync(string cookie, CancellationToken ct = default)
    {
        var cached = _csrfCache.Get(cookie);
        if (cached is not null) return cached;

        // POST to authentication-ticket without CSRF triggers 403 + token in response header.
        using var req = NewRequest(HttpMethod.Post, $"{_options.AuthBaseUrl}/v1/authentication-ticket/", cookie);
        req.Headers.TryAddWithoutValidation("Referer", _options.LauncherReferer);
        // Empty body + Content-Type: application/json — without these Roblox returns
        // 400/415 instead of the expected 403 with the token header.
        req.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, ct);
        if (res.Headers.TryGetValues("x-csrf-token", out var tokens))
        {
            var token = tokens.First();
            _csrfCache.Set(cookie, token);
            return token;
        }
        var body = await SafeReadBodyAsync(res, ct);
        _logger.LogWarning(
            "CSRF token request returned {StatusCode} without x-csrf-token header. body={Body}",
            (int)res.StatusCode, body.Length > 500 ? body[..500] + "..." : body);
        throw new RobloxApiException(
            $"Failed to obtain X-CSRF-TOKEN: HTTP {(int)res.StatusCode}",
            res.StatusCode, "auth/v1/authentication-ticket");
    }

    public async Task<string> GetAuthTicketAsync(string cookie, CancellationToken ct = default)
    {
        var endpoint = $"{_options.AuthBaseUrl}/v1/authentication-ticket/";

        // Roblox requires an empty JSON body + Content-Type: application/json on this POST.
        // Without Content-Type the response is a generic 400/403 instead of the 200+ticket header.
        var emptyBody = new StringContent("", System.Text.Encoding.UTF8, "application/json");

        using var res = await SendWithCsrfAsync(
            HttpMethod.Post,
            endpoint,
            cookie,
            req => req.Headers.TryAddWithoutValidation("Referer", _options.LauncherReferer),
            content: emptyBody,
            ct);

        if (!res.IsSuccessStatusCode)
        {
            var body = await SafeReadBodyAsync(res, ct);
            _logger.LogWarning(
                "Auth ticket request failed: {StatusCode} body={Body}",
                (int)res.StatusCode,
                body.Length > 500 ? body[..500] + "..." : body);
            throw new RobloxApiException(
                $"AuthTicket request failed: HTTP {(int)res.StatusCode}",
                res.StatusCode, endpoint);
        }
        if (!res.Headers.TryGetValues("rbx-authentication-ticket", out var values))
        {
            var headerNames = string.Join(", ", res.Headers.Select(h => h.Key));
            _logger.LogWarning(
                "Auth ticket response 200 but no rbx-authentication-ticket header. Headers: {Headers}",
                headerNames);
            throw new RobloxApiException(
                "AuthTicket response missing rbx-authentication-ticket header",
                res.StatusCode, endpoint);
        }
        return values.First();
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage res, CancellationToken ct)
    {
        try { return await res.Content.ReadAsStringAsync(ct); }
        catch { return "(could not read body)"; }
    }

    public async Task<IReadOnlyDictionary<ulong, UserPresence>> GetPresenceAsync(
        IReadOnlyCollection<ulong> userIds, string cookie, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<ulong, UserPresence>();

        var body = JsonContent.Create(new PresenceUsersRequest(userIds.ToArray()), options: JsonOptions);
        using var res = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"{_options.PresenceBaseUrl}/v1/presence/users",
            cookie,
            configure: null,
            content: body,
            ct);
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<PresenceUsersResponse>(JsonOptions, ct);
        if (dto is null) return new Dictionary<ulong, UserPresence>();

        return dto.UserPresences.ToDictionary(
            p => p.UserId,
            p => new UserPresence
            {
                UserId = p.UserId,
                Type = (PresenceType)p.UserPresenceType,
                PlaceId = p.PlaceId,
                RootPlaceId = p.RootPlaceId,
                UniverseId = p.UniverseId,
                GameId = p.GameId,
                LastOnline = p.LastOnline,
                LastLocation = p.LastLocation,
            });
    }

    public async Task<IReadOnlyDictionary<ThumbnailKey, ThumbnailResult>> GetThumbnailsAsync(
        IReadOnlyCollection<ThumbnailKey> requests, CancellationToken ct = default)
    {
        if (requests.Count == 0) return new Dictionary<ThumbnailKey, ThumbnailResult>();

        var body = requests.Select((k, i) => new ThumbnailBatchRequestItem(
            RequestId: $"{i}:{k.TargetId}:{k.Type}:{k.Size}",
            TargetId: k.TargetId,
            Type: k.Type,
            Size: k.Size,
            Format: k.Format)).ToArray();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.ThumbnailsBaseUrl}/v1/batch")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<ThumbnailBatchResponse>(JsonOptions, ct);
        if (dto is null) return new Dictionary<ThumbnailKey, ThumbnailResult>();

        var byTarget = requests.ToDictionary(k => $"{k.TargetId}:{k.Type}:{k.Size}", k => k);
        var result = new Dictionary<ThumbnailKey, ThumbnailResult>();
        foreach (var item in dto.Data)
        {
            var key = byTarget.Values.FirstOrDefault(k => k.TargetId == item.TargetId);
            if (key is null) continue;
            result[key] = new ThumbnailResult(item.ImageUrl, item.State);
        }
        return result;
    }

    public async Task<UniverseInfo?> GetUniverseFromPlaceAsync(ulong placeId, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{_options.ApisBaseUrl}/universes/v1/places/{placeId}/universe");
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        var u = await res.Content.ReadFromJsonAsync<UniverseFromPlaceDto>(JsonOptions, ct);
        if (u is null) return null;

        using var detailsReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_options.GamesBaseUrl}/v1/games?universeIds={u.UniverseId}");
        using var detailsRes = await _http.SendAsync(detailsReq, ct);
        if (!detailsRes.IsSuccessStatusCode) return new UniverseInfo(u.UniverseId, placeId, "");
        var details = await detailsRes.Content.ReadFromJsonAsync<GameDetailsDto>(JsonOptions, ct);
        var first = details?.Data.FirstOrDefault();
        return new UniverseInfo(u.UniverseId, first?.RootPlaceId ?? placeId, first?.Name ?? "");
    }

    public async Task<long> GetRobuxAsync(string cookie, CancellationToken ct = default)
    {
        using var req = NewRequest(HttpMethod.Get,
            $"{_options.EconomyBaseUrl}/v1/user/currency", cookie);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<RobuxBalanceDto>(JsonOptions, ct);
        return dto?.Robux ?? 0;
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string url,
        string cookie,
        Action<HttpRequestMessage>? configure,
        HttpContent? content,
        CancellationToken ct)
    {
        async Task<HttpResponseMessage> SendOnce(string? csrfToken)
        {
            var req = NewRequest(method, url, cookie);
            configure?.Invoke(req);
            if (csrfToken is not null) req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrfToken);
            if (content is not null) req.Content = await CloneContent(content);
            return await _http.SendAsync(req, ct);
        }

        var token = _csrfCache.Get(cookie);
        var res = await SendOnce(token);
        if (res.StatusCode == HttpStatusCode.Forbidden &&
            res.Headers.TryGetValues("x-csrf-token", out var fresh))
        {
            res.Dispose();
            var newToken = fresh.First();
            _csrfCache.Set(cookie, newToken);
            _logger.LogDebug("Refreshed X-CSRF-TOKEN and retrying {Url}", url);
            res = await SendOnce(newToken);
        }
        return res;
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string url, string? cookie = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (cookie is not null)
            req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
        return req;
    }

    private static async Task<HttpContent?> CloneContent(HttpContent content)
    {
        var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        ms.Position = 0;
        var clone = new StreamContent(ms);
        foreach (var h in content.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        return clone;
    }
}
