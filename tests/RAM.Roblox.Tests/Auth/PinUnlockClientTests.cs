using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAM.Roblox.Api;
using RAM.Roblox.Auth;
using RichardSzalay.MockHttp;

namespace RAM.Roblox.Tests.Auth;

public class PinUnlockClientTests
{
    private static PinUnlockClient Build(MockHttpMessageHandler mock) => new(
        TestHttpClient.Create(mock),
        Options.Create(new RobloxApiOptions()),
        new CsrfTokenCache(),
        NullLogger<PinUnlockClient>.Instance);

    [Fact]
    public async Task Unlock_returns_expiry_on_success()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/account/pin/unlock")
            .Respond(_ =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-1");
                return res;
            });
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/account/pin/unlock")
            .WithPartialContent("\"pin\":\"1234\"")
            .Respond(HttpStatusCode.OK);

        var client = Build(mock);
        var expiry = await client.UnlockAsync("test-cookie", "1234");
        Assert.NotNull(expiry);
        Assert.True(expiry > DateTimeOffset.UtcNow);
        Assert.True(expiry < DateTimeOffset.UtcNow + TimeSpan.FromMinutes(6));
    }

    [Fact]
    public async Task Unlock_returns_null_on_failure()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/account/pin/unlock")
            .Respond(_ =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-1");
                return res;
            });
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v1/account/pin/unlock")
            .Respond(HttpStatusCode.Unauthorized);

        var client = Build(mock);
        var expiry = await client.UnlockAsync("test-cookie", "9999");
        Assert.Null(expiry);
    }

    [Fact]
    public async Task Unlock_throws_on_non_4_digit_pin()
    {
        var mock = new MockHttpMessageHandler();
        var client = Build(mock);
        await Assert.ThrowsAsync<ArgumentException>(() => client.UnlockAsync("c", "12345"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UnlockAsync("c", "abcd"));
    }
}
