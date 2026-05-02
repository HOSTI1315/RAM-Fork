// Note: plan locked NSec.Cryptography, but NSec does not expose XSalsa20-Poly1305 (libsodium
// SecretBox), required for legacy RAM file reads. Using Sodium.Core gives us Argon2id KDF +
// SecretBox under one libsodium binding, with a smaller surface to test.
using Sodium;

namespace RAM.Storage.Crypto;

public sealed class SodiumArgon2idCipher
{
    public const int SaltBytes = 16;
    public const int KeyBytes = 32;       // SecretBox key = 32 bytes
    public const int NonceBytes = 24;     // XSalsa20 nonce = 24 bytes

    private const int DefaultOpsLimit = 4;
    private const int DefaultMemKb = 64 * 1024;   // 64 MiB

    public byte[] Encrypt(byte[] plaintext, string password)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = SodiumCore.GetRandomBytes(SaltBytes);
        var nonce = SodiumCore.GetRandomBytes(NonceBytes);
        var key = DeriveKey(password, salt, KdfVariant.Argon2id, DefaultOpsLimit, DefaultMemKb);
        try
        {
            var ct = SecretBox.Create(plaintext, nonce, key);
            return CryptoEnvelope.Pack(new CryptoEnvelope.Layout(
                Kdf: KdfVariant.Argon2id,
                OpsLimit: DefaultOpsLimit,
                MemKb: DefaultMemKb,
                Salt: salt,
                Nonce: nonce,
                Ciphertext: ct));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public byte[] Decrypt(byte[] envelope, string password)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrEmpty(password);

        var l = CryptoEnvelope.Unpack(envelope);
        var key = DeriveKey(password, l.Salt, l.Kdf, l.OpsLimit, l.MemKb);
        try
        {
            return SecretBox.Open(l.Ciphertext, l.Nonce, key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    internal static byte[] DeriveKey(string password, byte[] salt, KdfVariant variant, int ops, int memKb)
    {
        var alg = variant switch
        {
            KdfVariant.Argon2id => PasswordHash.ArgonAlgorithm.Argon_2ID13,
            KdfVariant.Argon2i  => PasswordHash.ArgonAlgorithm.Argon_2I13,
            _ => throw new NotSupportedException($"Unknown KDF variant: {variant}"),
        };
        return PasswordHash.ArgonHashBinary(
            password: System.Text.Encoding.UTF8.GetBytes(password),
            salt: salt,
            opsLimit: (long)ops,
            memLimit: memKb * 1024,
            outputLength: (long)KeyBytes,
            alg: alg);
    }
}

internal static class CryptographicOperations
{
    public static void ZeroMemory(byte[] data) => System.Security.Cryptography.CryptographicOperations.ZeroMemory(data);
}
