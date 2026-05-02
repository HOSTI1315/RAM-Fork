using RichardSzalay.MockHttp;

namespace RAM.Roblox.Tests;

/// <summary>
/// Wraps a <see cref="MockHttpMessageHandler"/> as an <see cref="HttpClient"/> ready
/// for direct injection into <see cref="RAM.Roblox.Api.RobloxApi"/>.
/// </summary>
internal static class TestHttpClient
{
    public static HttpClient Create(MockHttpMessageHandler mock)
        => new(mock) { Timeout = TimeSpan.FromSeconds(5) };
}
