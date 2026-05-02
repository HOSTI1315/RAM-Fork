using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace RAM.Roblox.Api;

/// <summary>
/// Per-cookie X-CSRF-TOKEN cache. Cookies are SHA-256 hashed before use as keys so the
/// raw cookie never lives outside the call site.
/// </summary>
public sealed class CsrfTokenCache
{
    private readonly ConcurrentDictionary<string, string> _tokens = new();

    public string? Get(string cookie)
    {
        var key = HashCookie(cookie);
        return _tokens.TryGetValue(key, out var token) ? token : null;
    }

    public void Set(string cookie, string token) =>
        _tokens[HashCookie(cookie)] = token;

    public void Invalidate(string cookie) =>
        _tokens.TryRemove(HashCookie(cookie), out _);

    private static string HashCookie(string cookie)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(cookie), hash);
        return Convert.ToHexString(hash);
    }
}
