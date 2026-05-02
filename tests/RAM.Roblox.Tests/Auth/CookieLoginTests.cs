using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAM.Roblox.Api;
using RAM.Roblox.Auth;
using RichardSzalay.MockHttp;

namespace RAM.Roblox.Tests.Auth;

public class CookieLoginTests
{
    [Fact]
    public async Task Validate_returns_success_for_authenticated_user()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated")
            .Respond("application/json", "{\"id\":42,\"name\":\"alice\",\"displayName\":\"Alice A\"}");

        var api = new RobloxApi(
            TestHttpClient.Create(mock),
            Options.Create(new RobloxApiOptions()),
            new CsrfTokenCache(),
            NullLogger<RobloxApi>.Instance);
        var login = new CookieLogin(api, NullLogger<CookieLogin>.Instance);

        var result = await login.ValidateAsync("test-cookie");
        var success = Assert.IsType<LoginResult.Success>(result);
        Assert.Equal(42ul, success.UserId);
        Assert.Equal("alice", success.Username);
        Assert.Equal("Alice A", success.DisplayName);
    }

    [Fact]
    public async Task Validate_returns_failed_on_unauthorized()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated")
            .Respond(HttpStatusCode.Unauthorized);

        var api = new RobloxApi(
            TestHttpClient.Create(mock),
            Options.Create(new RobloxApiOptions()),
            new CsrfTokenCache(),
            NullLogger<RobloxApi>.Instance);
        var login = new CookieLogin(api, NullLogger<CookieLogin>.Instance);

        var result = await login.ValidateAsync("bad-cookie");
        Assert.IsType<LoginResult.Failed>(result);
    }

    [Fact]
    public async Task Validate_returns_failed_on_empty_cookie()
    {
        var login = new CookieLogin(
            new RobloxApi(new HttpClient(), Options.Create(new RobloxApiOptions()),
                new CsrfTokenCache(), NullLogger<RobloxApi>.Instance),
            NullLogger<CookieLogin>.Instance);
        var result = await login.ValidateAsync("");
        Assert.IsType<LoginResult.Failed>(result);
    }
}
