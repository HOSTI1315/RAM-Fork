using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAM.Roblox.Api;
using RAM.Roblox.Auth;
using RichardSzalay.MockHttp;

namespace RAM.Roblox.Tests.Auth;

public class PasswordLoginTests
{
    private static PasswordLogin Build(MockHttpMessageHandler mock)
    {
        return new PasswordLogin(
            TestHttpClient.Create(mock),
            Options.Create(new RobloxApiOptions()),
            new CsrfTokenCache(),
            NullLogger<PasswordLogin>.Instance);
    }

    [Fact]
    public async Task Successful_login_returns_cookie_from_set_cookie_header()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v2/login")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-1");
                return res;
            });
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v2/login")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.OK);
                res.Headers.Add("Set-Cookie",
                    ".ROBLOSECURITY=COOKIE_OK_VALUE; path=/; domain=.roblox.com");
                res.Content = new StringContent(
                    "{\"user\":{\"id\":7,\"name\":\"alice\",\"displayName\":\"Alice\"}}",
                    System.Text.Encoding.UTF8, "application/json");
                return res;
            });

        var login = Build(mock);
        var result = await login.StartAsync("alice", "password123");
        var success = Assert.IsType<LoginResult.Success>(result);
        Assert.Equal("COOKIE_OK_VALUE", success.Cookie);
        Assert.Equal(7ul, success.UserId);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Two_factor_required_returns_challenge()
    {
        var mock = new MockHttpMessageHandler();
        // CSRF dance
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v2/login")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-1");
                return res;
            });
        // Login attempt → 403 with 2FA payload
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v2/login")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Content = new StringContent(
                    "{\"user\":{\"id\":42,\"name\":\"alice\",\"displayName\":\"Alice\"}," +
                    "\"twoStepVerificationData\":{\"mediaType\":\"Email\",\"ticket\":\"TICKET-XYZ\"}}",
                    System.Text.Encoding.UTF8, "application/json");
                return res;
            });

        var login = Build(mock);
        var result = await login.StartAsync("alice", "password123");
        var twoFa = Assert.IsType<LoginResult.TwoFactorRequired>(result);
        Assert.Equal(42ul, twoFa.UserId);
        Assert.Equal("TICKET-XYZ", twoFa.ChallengeId);
        Assert.Equal(TwoFactorMediaType.Email, twoFa.MediaType);
    }

    [Fact]
    public async Task Failed_login_returns_status_code()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v2/login")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-1");
                return res;
            });
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v2/login")
            .Respond(HttpStatusCode.Unauthorized);

        var login = Build(mock);
        var result = await login.StartAsync("alice", "wrong");
        var failed = Assert.IsType<LoginResult.Failed>(result);
        Assert.Equal(401, failed.StatusCode);
    }

    [Fact]
    public async Task Two_factor_completion_chains_verify_then_login()
    {
        var mock = new MockHttpMessageHandler();
        // Verify endpoint
        mock.Expect(HttpMethod.Post,
                "https://twostepverification.roblox.com/v1/users/42/challenges/email-code/verify")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.Forbidden);
                res.Headers.Add("x-csrf-token", "csrf-2fa");
                return res;
            });
        mock.Expect(HttpMethod.Post,
                "https://twostepverification.roblox.com/v1/users/42/challenges/email-code/verify")
            .WithPartialContent("\"code\":\"123456\"")
            .Respond("application/json", "{\"verificationToken\":\"VTOKEN-ABC\"}");

        // Final login: still needs CSRF — keep dance simple by accepting same csrf
        mock.Expect(HttpMethod.Post, "https://auth.roblox.com/v2/login")
            .Respond(req =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.OK);
                res.Headers.Add("Set-Cookie", ".ROBLOSECURITY=FINAL_COOKIE; path=/");
                res.Content = new StringContent(
                    "{\"user\":{\"id\":42,\"name\":\"alice\",\"displayName\":\"Alice\"}}",
                    System.Text.Encoding.UTF8, "application/json");
                return res;
            });

        var login = Build(mock);
        var result = await login.CompleteTwoFactorAsync(
            42, TwoFactorMediaType.Email, "TICKET-XYZ", "123456");
        var success = Assert.IsType<LoginResult.Success>(result);
        Assert.Equal("FINAL_COOKIE", success.Cookie);
        mock.VerifyNoOutstandingExpectation();
    }
}
