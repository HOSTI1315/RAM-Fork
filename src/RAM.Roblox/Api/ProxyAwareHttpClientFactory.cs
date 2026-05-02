using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using RAM.Core.Models;

namespace RAM.Roblox.Api;

/// <summary>
/// Per-account proxy-aware HttpClient factory. Caches <see cref="SocketsHttpHandler"/>
/// instances keyed by a hash of the account's <see cref="ProxyConfig"/>; new clients
/// for unchanged proxies share a handler (DNS / connection pool reuse). Changing the
/// proxy on an account flips the hash → next CreateClient builds a fresh handler.
///
/// <para>.NET 6+ has native SOCKS support via <see cref="WebProxy"/> with
/// <c>socks5://</c> / <c>socks4://</c> scheme — no custom DelegatingHandler needed.</para>
/// </summary>
public sealed class ProxyAwareHttpClientFactory : IDisposable
{
    private readonly ConcurrentDictionary<string, SocketsHttpHandler> _handlers = new();
    private bool _disposed;

    /// <summary>Number of cached handlers (one per distinct proxy config + a "none" slot).</summary>
    public int HandlerCount => _handlers.Count;

    public HttpClient CreateClient(Account account) => CreateClient(account.Proxy);

    public HttpClient CreateClient(ProxyConfig? proxy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = ComputeKey(proxy);
        var handler = _handlers.GetOrAdd(key, _ => BuildHandler(proxy));
        // disposeHandler:false — handler is cached and shared; only dispose via Dispose().
        return new HttpClient(handler, disposeHandler: false);
    }

    /// <summary>Test hook: returns the cache key for a proxy config.</summary>
    public static string ComputeKey(ProxyConfig? proxy)
    {
        if (proxy is null) return "no-proxy";
        // SHA-256 over a stable canonical form of all fields (including password). Hex-encoded
        // → fixed-length, content-addressed key. Never logged in plaintext.
        var canonical =
            $"{(int)proxy.Type}|{proxy.Host}|{proxy.Port}|{proxy.Username ?? ""}|{proxy.Password ?? ""}";
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(canonical), hash);
        return Convert.ToHexString(hash);
    }

    private static SocketsHttpHandler BuildHandler(ProxyConfig? proxy)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (proxy is null)
        {
            handler.UseProxy = false;
            return handler;
        }

        var scheme = proxy.Type switch
        {
            ProxyType.Http => "http",
            ProxyType.Socks4 => "socks4",
            ProxyType.Socks5 => "socks5",
            _ => "http",
        };
        var webProxy = new WebProxy($"{scheme}://{proxy.Host}:{proxy.Port}");
        if (!string.IsNullOrEmpty(proxy.Username))
            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password ?? string.Empty);

        handler.Proxy = webProxy;
        handler.UseProxy = true;
        return handler;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var h in _handlers.Values) h.Dispose();
        _handlers.Clear();
    }
}
