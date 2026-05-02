using System.Net;
using System.Net.Sockets;
using System.Text;
using RAM.Core.Models;
using RAM.Roblox.Api;

namespace RAM.Roblox.Tests.Api;

public class ProxyAwareHttpClientFactoryTests
{
    [Fact]
    public void Same_proxy_config_yields_same_cache_key()
    {
        var p1 = new ProxyConfig(ProxyType.Socks5, "host", 1080, "u", "pw");
        var p2 = new ProxyConfig(ProxyType.Socks5, "host", 1080, "u", "pw");
        Assert.Equal(ProxyAwareHttpClientFactory.ComputeKey(p1),
                      ProxyAwareHttpClientFactory.ComputeKey(p2));
    }

    [Fact]
    public void Different_proxy_password_yields_different_cache_key()
    {
        var p1 = new ProxyConfig(ProxyType.Http, "h", 8080, "u", "old");
        var p2 = new ProxyConfig(ProxyType.Http, "h", 8080, "u", "new");
        Assert.NotEqual(ProxyAwareHttpClientFactory.ComputeKey(p1),
                         ProxyAwareHttpClientFactory.ComputeKey(p2));
    }

    [Fact]
    public void No_proxy_yields_stable_no_proxy_key()
    {
        Assert.Equal("no-proxy", ProxyAwareHttpClientFactory.ComputeKey(null));
    }

    [Fact]
    public void CreateClient_caches_handler_per_config()
    {
        using var factory = new ProxyAwareHttpClientFactory();
        var account = new Account
        {
            UserId = 1, Username = "u", Cookie = "c",
            Proxy = new ProxyConfig(ProxyType.Http, "h", 8080),
        };

        _ = factory.CreateClient(account);
        _ = factory.CreateClient(account);
        _ = factory.CreateClient(account);

        Assert.Equal(1, factory.HandlerCount);
    }

    [Fact]
    public void CreateClient_builds_new_handler_when_proxy_changes()
    {
        using var factory = new ProxyAwareHttpClientFactory();
        var noProxy = new Account { UserId = 1, Username = "u", Cookie = "c" };
        var withProxy = noProxy with { Proxy = new ProxyConfig(ProxyType.Http, "h", 8080) };

        _ = factory.CreateClient(noProxy);
        _ = factory.CreateClient(withProxy);

        Assert.Equal(2, factory.HandlerCount);
    }

    [Fact]
    public async Task Client_routes_HTTP_request_through_configured_proxy()
    {
        // Stand up a TCP listener that just captures the first request line. A real HTTP
        // proxy gets either "GET http://target/path HTTP/1.1" (origin-form) or "CONNECT
        // target:port HTTP/1.1" — we only need to confirm the request hit the proxy.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var serverCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var captureTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(serverCts.Token);
            using var stream = client.GetStream();
            var buffer = new byte[2048];
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), serverCts.Token);
            return Encoding.ASCII.GetString(buffer, 0, read);
        });

        var account = new Account
        {
            UserId = 1, Username = "u", Cookie = "c",
            Proxy = new ProxyConfig(ProxyType.Http, "127.0.0.1", port),
        };

        using var factory = new ProxyAwareHttpClientFactory();
        using var http = factory.CreateClient(account);
        http.Timeout = TimeSpan.FromSeconds(2);

        // The request will fail (we never write a response) — we only care that the
        // proxy received it.
        try { await http.GetAsync("http://example.invalid/test-path"); }
        catch { /* expected — proxy never replies */ }

        var captured = await captureTask;
        Assert.Contains("example.invalid", captured);
        Assert.Contains("test-path", captured);
    }

    [Fact]
    public void Disposed_factory_throws_on_CreateClient()
    {
        var factory = new ProxyAwareHttpClientFactory();
        factory.Dispose();
        Assert.Throws<ObjectDisposedException>(() =>
            factory.CreateClient(new Account { UserId = 1, Username = "u", Cookie = "c" }));
    }

    [Fact]
    public void Disposing_factory_disposes_cached_handlers()
    {
        var factory = new ProxyAwareHttpClientFactory();
        var account = new Account { UserId = 1, Username = "u", Cookie = "c" };
        _ = factory.CreateClient(account);
        Assert.Equal(1, factory.HandlerCount);
        factory.Dispose();
        Assert.Equal(0, factory.HandlerCount);
    }
}
