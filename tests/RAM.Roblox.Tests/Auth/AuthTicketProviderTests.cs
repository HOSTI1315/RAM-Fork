using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAM.Roblox.Api;
using RAM.Roblox.Auth;
using RichardSzalay.MockHttp;

namespace RAM.Roblox.Tests.Auth;

public class AuthTicketProviderTests
{
    [Fact]
    public async Task Get_returns_ticket_from_response_header_after_csrf()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/")
            .Respond(_ =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-A");
                return res;
            });
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/")
            .Respond(_ =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.OK);
                res.Headers.Add("rbx-authentication-ticket", "AUTHTKT-1234");
                return res;
            });

        var api = new RobloxApi(
            TestHttpClient.Create(mock),
            Options.Create(new RobloxApiOptions()),
            new CsrfTokenCache(),
            NullLogger<RobloxApi>.Instance);

        var provider = new AuthTicketProvider(api, NullLogger<AuthTicketProvider>.Instance);
        var ticket = await provider.GetAsync("test-cookie");
        Assert.Equal("AUTHTKT-1234", ticket);
    }
}
