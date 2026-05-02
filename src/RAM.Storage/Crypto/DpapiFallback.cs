using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace RAM.Storage.Crypto;

/// <summary>
/// Reads Windows DPAPI-encrypted legacy files (pre-Argon2i RAM versions).
/// Returns null if the data is not DPAPI-encrypted or cannot be unprotected
/// under the current user.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiFallback
{
    public byte[]? TryUnprotect(byte[] data, byte[]? entropy = null)
    {
        try
        {
            return ProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public byte[] Protect(byte[] data, byte[]? entropy = null)
        => ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
}
