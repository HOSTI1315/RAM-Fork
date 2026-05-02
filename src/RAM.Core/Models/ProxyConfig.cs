namespace RAM.Core.Models;

/// <summary>
/// Per-account proxy configuration. Stored inside the <see cref="Account"/> record and
/// encrypted alongside the cookie via the standard account-store envelope (no separate
/// credential store). Password is excluded from plaintext exports.
/// </summary>
public sealed record ProxyConfig(
    ProxyType Type,
    string Host,
    int Port,
    string? Username = null,
    string? Password = null);

public enum ProxyType
{
    Http = 0,
    Socks4 = 1,
    Socks5 = 2,
}
