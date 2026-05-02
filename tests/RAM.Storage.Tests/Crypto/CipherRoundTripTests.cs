using System.Text;
using RAM.Storage.Crypto;

namespace RAM.Storage.Tests.Crypto;

public class CipherRoundTripTests
{
    [Fact]
    public void Encrypt_then_decrypt_returns_original_plaintext()
    {
        var cipher = new SodiumArgon2idCipher();
        var plaintext = Encoding.UTF8.GetBytes("hello roblox account manager");
        var envelope = cipher.Encrypt(plaintext, "correct horse battery staple");
        var decrypted = cipher.Decrypt(envelope, "correct horse battery staple");
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_with_wrong_password_throws()
    {
        var cipher = new SodiumArgon2idCipher();
        var envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("data"), "right");
        Assert.ThrowsAny<Exception>(() => cipher.Decrypt(envelope, "wrong"));
    }

    [Fact]
    public void Envelope_starts_with_RAM2_magic()
    {
        var cipher = new SodiumArgon2idCipher();
        var envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("x"), "pw");
        Assert.True(CryptoEnvelope.LooksLikeRam2(envelope));
        Assert.Equal((byte)KdfVariant.Argon2id, envelope[5]);
    }

    [Fact]
    public void Different_encryptions_produce_different_envelopes()
    {
        var cipher = new SodiumArgon2idCipher();
        var pt = Encoding.UTF8.GetBytes("same plaintext");
        var a = cipher.Encrypt(pt, "pw");
        var b = cipher.Encrypt(pt, "pw");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Argon2i_legacy_envelope_is_readable_via_legacy_reader()
    {
        // Build a synthetic legacy envelope: same format, but with KdfVariant.Argon2i.
        // The reader uses libsodium for both KDFs so this exercises the migration path.
        var password = "legacy-pw";
        var salt = Sodium.SodiumCore.GetRandomBytes(SodiumArgon2idCipher.SaltBytes);
        var nonce = Sodium.SodiumCore.GetRandomBytes(SodiumArgon2idCipher.NonceBytes);
        var key = SodiumArgon2idCipher.DeriveKey(password, salt, KdfVariant.Argon2i, ops: 4, memKb: 64 * 1024);
        var pt = Encoding.UTF8.GetBytes("legacy data");
        var ct = Sodium.SecretBox.Create(pt, nonce, key);
        var legacyEnvelope = CryptoEnvelope.Pack(new CryptoEnvelope.Layout(
            Kdf: KdfVariant.Argon2i,
            OpsLimit: 4,
            MemKb: 64 * 1024,
            Salt: salt,
            Nonce: nonce,
            Ciphertext: ct));

        var reader = new Argon2iLegacyReader();
        var recovered = reader.TryDecrypt(legacyEnvelope, password);
        Assert.NotNull(recovered);
        Assert.Equal(pt, recovered);
    }

    [Fact]
    public void Legacy_reader_returns_null_on_argon2id_envelope()
    {
        var cipher = new SodiumArgon2idCipher();
        var envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("x"), "pw");
        var reader = new Argon2iLegacyReader();
        Assert.Null(reader.TryDecrypt(envelope, "pw"));
    }

    [Fact]
    public void Legacy_reader_returns_null_on_garbage()
    {
        var reader = new Argon2iLegacyReader();
        Assert.Null(reader.TryDecrypt(new byte[] { 1, 2, 3, 4, 5 }, "pw"));
    }
}
