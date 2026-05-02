namespace RAM.Storage.Crypto;

/// <summary>
/// Reads legacy RAM-format Argon2i + SecretBox files. Re-uses the same envelope; the
/// only delta is <see cref="KdfVariant.Argon2i"/> in the header. After successful read,
/// call <see cref="SodiumArgon2idCipher.Encrypt"/> to re-write under Argon2id.
/// </summary>
public sealed class Argon2iLegacyReader
{
    private readonly SodiumArgon2idCipher _cipher = new();

    public byte[]? TryDecrypt(byte[] envelope, string password)
    {
        if (!CryptoEnvelope.LooksLikeRam2(envelope))
            return null;

        try
        {
            var layout = CryptoEnvelope.Unpack(envelope);
            if (layout.Kdf != KdfVariant.Argon2i)
                return null;
            return _cipher.Decrypt(envelope, password);
        }
        catch
        {
            return null;
        }
    }
}
