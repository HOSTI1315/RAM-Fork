using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Api;
using RichardSzalay.MockHttp;

namespace RAM.Roblox.Tests.Api;

public class RobloxApiTests
{
    private const string TestCookie = "test_cookie_value";

    private static (RobloxApi api, MockHttpMessageHandler mock) Build()
    {
        var mock = new MockHttpMessageHandler();
        var http = TestHttpClient.Create(mock);
        var api = new RobloxApi(
            http,
            Options.Create(new RobloxApiOptions()),
            new CsrfTokenCache(),
            NullLogger<RobloxApi>.Instance);
        return (api, mock);
    }

    [Fact]
    public async Task ValidateCookie_returns_true_for_authenticated_user()
    {
        var (api, mock) = Build();
        mock.When(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated")
            .Respond("application/json",
                "{\"id\":12345,\"name\":\"alice\",\"displayName\":\"Alice\"}");

        Assert.True(await api.ValidateCookieAsync(TestCookie));
    }

    [Fact]
    public async Task ValidateCookie_returns_false_on_unauthorized()
    {
        var (api, mock) = Build();
        mock.When(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated")
            .Respond(HttpStatusCode.Unauthorized);

        Assert.False(await api.ValidateCookieAsync(TestCookie));
    }

    [Fact]
    public async Task GetAuthenticatedUser_parses_response()
    {
        var (api, mock) = Build();
        mock.When(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated")
            .Respond("application/json",
                "{\"id\":777,\"name\":\"bob\",\"displayName\":\"Bobster\"}");

        var user = await api.GetAuthenticatedUserAsync(TestCookie);
        Assert.NotNull(user);
        Assert.Equal(777ul, user!.Id);
        Assert.Equal("Bobster", user.DisplayName);
    }

    [Fact]
    public async Task GetAuthTicket_does_csrf_dance_and_returns_ticket()
    {
        var (api, mock) = Build();

        // First POST → 403 + X-CSRF-TOKEN header
        var first = mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/")
            .With(r => !r.Headers.Contains("X-CSRF-TOKEN"))
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-A1B2");
                return res;
            });

        // Retry POST with token → 200 + rbx-authentication-ticket header
        var second = mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/")
            .With(r => r.Headers.TryGetValues("X-CSRF-TOKEN", out var v) && v.First() == "csrf-A1B2")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.OK);
                res.Headers.Add("rbx-authentication-ticket", "TICKET-12345");
                return res;
            });

        var ticket = await api.GetAuthTicketAsync(TestCookie);
        Assert.Equal("TICKET-12345", ticket);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetAuthTicket_throws_when_response_lacks_ticket_header()
    {
        var (api, mock) = Build();
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "x");
                return res;
            });
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/")
            .Respond(HttpStatusCode.OK); // no ticket header

        await Assert.ThrowsAsync<RobloxApiException>(() => api.GetAuthTicketAsync(TestCookie));
    }

    [Fact]
    public async Task GetPresence_batches_user_ids_into_single_request()
    {
        var (api, mock) = Build();
        // First request gets CSRF
        mock.Expect(HttpMethod.Post, "https://presence.roblox.com/v1/presence/users")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-1");
                return res;
            });
        // Retry returns presence data
        mock.Expect(HttpMethod.Post, "https://presence.roblox.com/v1/presence/users")
            .WithPartialContent("\"userIds\":[1,2,3]")
            .Respond("application/json", PresenceJson);

        var presences = await api.GetPresenceAsync(new ulong[] { 1, 2, 3 }, TestCookie);
        Assert.Equal(3, presences.Count);
        Assert.Equal(PresenceType.InGame, presences[1].Type);
        Assert.Equal(99ul, presences[1].PlaceId);
        Assert.Equal(PresenceType.Online, presences[2].Type);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPresence_short_circuits_on_empty_input()
    {
        var (api, _) = Build();
        var result = await api.GetPresenceAsync(Array.Empty<ulong>(), TestCookie);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetThumbnails_returns_map_keyed_by_request()
    {
        var (api, mock) = Build();
        mock.When(HttpMethod.Post, "https://thumbnails.roblox.com/v1/batch")
            .Respond("application/json", ThumbnailJson);

        var requests = new[]
        {
            new ThumbnailKey(42, "AvatarHeadShot", "150x150"),
            new ThumbnailKey(43, "AvatarHeadShot", "150x150"),
        };
        var result = await api.GetThumbnailsAsync(requests);

        Assert.Equal(2, result.Count);
        Assert.Equal("https://cdn/42.png", result[requests[0]].ImageUrl);
        Assert.Equal("Completed", result[requests[0]].State);
    }

    private const string PresenceJson =
        "{\"userPresences\":[" +
        "{\"userId\":1,\"userPresenceType\":2,\"placeId\":99,\"universeId\":42,\"lastLocation\":\"In Game\",\"gameId\":null,\"rootPlaceId\":99,\"lastOnline\":null}," +
        "{\"userId\":2,\"userPresenceType\":1,\"placeId\":null,\"universeId\":null,\"lastLocation\":\"Online\",\"gameId\":null,\"rootPlaceId\":null,\"lastOnline\":null}," +
        "{\"userId\":3,\"userPresenceType\":0,\"placeId\":null,\"universeId\":null,\"lastLocation\":\"Offline\",\"gameId\":null,\"rootPlaceId\":null,\"lastOnline\":null}" +
        "]}";

    private const string ThumbnailJson =
        "{\"data\":[" +
        "{\"requestId\":\"0:42:AvatarHeadShot:150x150\",\"targetId\":42,\"state\":\"Completed\",\"imageUrl\":\"https://cdn/42.png\"}," +
        "{\"requestId\":\"1:43:AvatarHeadShot:150x150\",\"targetId\":43,\"state\":\"Completed\",\"imageUrl\":\"https://cdn/43.png\"}" +
        "]}";

    [Fact]
    public async Task GetUniverseFromPlace_returns_null_when_endpoint_404s()
    {
        var (api, mock) = Build();
        mock.When(HttpMethod.Get, "https://apis.roblox.com/universes/v1/places/606849621/universe")
            .Respond(HttpStatusCode.NotFound);

        var info = await api.GetUniverseFromPlaceAsync(606849621);
        Assert.Null(info);
    }

    [Fact]
    public async Task GetUniverseFromPlace_returns_universe_with_details()
    {
        var (api, mock) = Build();
        mock.When(HttpMethod.Get, "https://apis.roblox.com/universes/v1/places/606849621/universe")
            .Respond("application/json", "{\"universeId\":220851708}");
        mock.When(HttpMethod.Get, "https://games.roblox.com/v1/games?universeIds=220851708")
            .Respond("application/json",
                "{\"data\":[{\"id\":220851708,\"rootPlaceId\":606849621,\"name\":\"Jailbreak\"}]}");

        var info = await api.GetUniverseFromPlaceAsync(606849621);
        Assert.NotNull(info);
        Assert.Equal(220851708ul, info!.UniverseId);
        Assert.Equal("Jailbreak", info.Name);
    }

    [Fact]
    public async Task GetCsrfToken_uses_cache_on_second_call()
    {
        var (api, mock) = Build();
        var cache = new CsrfTokenCache();
        var http = TestHttpClient.Create(mock);
        var cachedApi = new RobloxApi(
            http, Options.Create(new RobloxApiOptions()), cache,
            NullLogger<RobloxApi>.Instance);

        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "first-token");
                return res;
            });

        var t1 = await cachedApi.GetCsrfTokenAsync(TestCookie);
        var t2 = await cachedApi.GetCsrfTokenAsync(TestCookie);
        Assert.Equal("first-token", t1);
        Assert.Equal("first-token", t2);
        // Only one HTTP request should have been made — second came from cache
        mock.VerifyNoOutstandingExpectation();
    }
}
