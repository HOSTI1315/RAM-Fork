namespace RAM.Storage.Crypto;

/// <summary>
/// Binary envelope for encrypted account data. Layout:
/// <code>
///   magic       4 bytes   "RAM2"
///   version     1 byte    currently 0x01
///   kdf         1 byte    KdfVariant (0 = argon2id, 1 = argon2i)
///   ops         4 bytes   little-endian, KDF iterations
///   memKb       4 bytes   little-endian, KDF memory in KiB
///   saltLen     1 byte    typically 16
///   salt        N bytes
///   nonceLen    1 byte    typically 24 (XSalsa20-Poly1305)
///   nonce       N bytes
///   ctLen       4 bytes   little-endian
///   ciphertext  N bytes   includes Poly1305 tag (libsodium SecretBox format)
/// </code>
/// </summary>
public static class CryptoEnvelope
{
    public static readonly byte[] Magic = "RAM2"u8.ToArray();
    public const byte CurrentVersion = 0x01;

    public sealed record Layout(
        KdfVariant Kdf,
        int OpsLimit,
        int MemKb,
        byte[] Salt,
        byte[] Nonce,
        byte[] Ciphertext);

    public static byte[] Pack(Layout l)
    {
        using var ms = new MemoryStream();
        ms.Write(Magic);
        ms.WriteByte(CurrentVersion);
        ms.WriteByte((byte)l.Kdf);
        WriteI32(ms, l.OpsLimit);
        WriteI32(ms, l.MemKb);
        ms.WriteByte((byte)l.Salt.Length);
        ms.Write(l.Salt);
        ms.WriteByte((byte)l.Nonce.Length);
        ms.Write(l.Nonce);
        WriteI32(ms, l.Ciphertext.Length);
        ms.Write(l.Ciphertext);
        return ms.ToArray();
    }

    public static Layout Unpack(byte[] data)
    {
        using var ms = new MemoryStream(data);
        var magic = new byte[4];
        if (ms.Read(magic) != 4 || !magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Not a RAM2 envelope.");
        var version = ms.ReadByte();
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported envelope version: {version}.");
        var kdf = (KdfVariant)ms.ReadByte();
        var ops = ReadI32(ms);
        var memKb = ReadI32(ms);
        var saltLen = ms.ReadByte();
        var salt = new byte[saltLen];
        ms.ReadExactly(salt);
        var nonceLen = ms.ReadByte();
        var nonce = new byte[nonceLen];
        ms.ReadExactly(nonce);
        var ctLen = ReadI32(ms);
        var ct = new byte[ctLen];
        ms.ReadExactly(ct);
        return new Layout(kdf, ops, memKb, salt, nonce, ct);
    }

    public static bool LooksLikeRam2(ReadOnlySpan<byte> data)
        => data.Length >= 4 && data[..4].SequenceEqual(Magic);

    private static void WriteI32(Stream s, int value)
    {
        Span<byte> b = stackalloc byte[4];
        BitConverter.TryWriteBytes(b, value);
        if (!BitConverter.IsLittleEndian) b.Reverse();
        s.Write(b);
    }

    private static int ReadI32(Stream s)
    {
        Span<byte> b = stackalloc byte[4];
        s.ReadExactly(b);
        if (!BitConverter.IsLittleEndian) b.Reverse();
        return BitConverter.ToInt32(b);
    }
}
