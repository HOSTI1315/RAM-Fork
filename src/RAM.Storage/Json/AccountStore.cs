using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Storage.Crypto;

namespace RAM.Storage.Json;

public sealed class AccountStore : IAccountStore
{
    private const string FileName = "AccountData.json";
    private const string BackupExtension = ".backup";
    private const int BackupRetentionHoursDefault = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDataDirectoryProvider _data;
    private readonly SodiumArgon2idCipher _cipher;
    private readonly Argon2iLegacyReader _legacyArgon2i;
    private readonly DpapiFallback _dpapi;
    private readonly ILogger<AccountStore> _logger;
    private readonly Func<string> _passwordProvider;

    public AccountStore(
        IDataDirectoryProvider data,
        SodiumArgon2idCipher cipher,
        Argon2iLegacyReader legacyArgon2i,
        DpapiFallback dpapi,
        Func<string> passwordProvider,
        ILogger<AccountStore> logger)
    {
        _data = data;
        _cipher = cipher;
        _legacyArgon2i = legacyArgon2i;
        _dpapi = dpapi;
        _passwordProvider = passwordProvider;
        _logger = logger;
    }

    public string FilePath => Path.Combine(_data.DataDirectory, FileName);

    public async Task<IReadOnlyList<Account>> LoadAllAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath))
        {
            _logger.LogInformation("AccountData.json not found, starting with empty store");
            return Array.Empty<Account>();
        }

        var bytes = await File.ReadAllBytesAsync(FilePath, ct);
        var plaintext = TryDecryptChain(bytes);
        if (plaintext is null)
            throw new InvalidDataException("Could not decrypt AccountData.json under any known scheme.");

        try
        {
            var envelope = JsonSerializer.Deserialize<AccountEnvelope>(plaintext, JsonOptions)
                ?? throw new InvalidDataException("Account envelope deserialized to null.");
            ValidateSchema(envelope.SchemaVersion);
            return envelope.Accounts;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task SaveAllAsync(IReadOnlyList<Account> accounts, CancellationToken ct = default)
    {
        var envelope = new AccountEnvelope { Accounts = accounts };
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        try
        {
            var ciphertext = _cipher.Encrypt(json, _passwordProvider());
            await RotateBackupIfDueAsync(ct);
            await WriteAtomicAsync(FilePath, ciphertext, ct);
            _logger.LogDebug("Saved {Count} accounts to AccountData.json", accounts.Count);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(json);
        }
    }

    private byte[]? TryDecryptChain(byte[] data)
    {
        if (CryptoEnvelope.LooksLikeRam2(data))
        {
            try
            {
                var pt = _cipher.Decrypt(data, _passwordProvider());
                _logger.LogDebug("Decrypted AccountData.json with current Argon2id cipher");
                return pt;
            }
            catch
            {
                var legacy = _legacyArgon2i.TryDecrypt(data, _passwordProvider());
                if (legacy is not null)
                {
                    _logger.LogInformation("Decrypted legacy Argon2i file; will be re-encrypted as Argon2id on next save");
                    return legacy;
                }
            }
        }

        var dpapi = _dpapi.TryUnprotect(data);
        if (dpapi is not null)
        {
            _logger.LogInformation("Decrypted legacy DPAPI file; will be re-encrypted as Argon2id on next save");
            return dpapi;
        }

        return null;
    }

    private static void ValidateSchema(int version)
    {
        if (version > AccountEnvelope.CurrentSchemaVersion)
            throw new InvalidDataException(
                $"AccountData.json schema_version={version} is newer than this build supports " +
                $"(max {AccountEnvelope.CurrentSchemaVersion}). Please update RAM or restore from a backup.");
    }

    private async Task RotateBackupIfDueAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath)) return;

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(_data.BackupsDirectory, $"AccountData_{stamp}{BackupExtension}");

        var lastBackup = Directory.EnumerateFiles(_data.BackupsDirectory, $"AccountData_*{BackupExtension}")
            .Select(p => File.GetCreationTimeUtc(p))
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
        if (DateTime.UtcNow - lastBackup < TimeSpan.FromHours(BackupRetentionHoursDefault))
            return;

        await using var src = File.OpenRead(FilePath);
        await using var dst = File.Create(backupPath);
        await src.CopyToAsync(dst, ct);
        _logger.LogDebug("Rotated AccountData backup → {BackupPath}", backupPath);
    }

    private static async Task WriteAtomicAsync(string path, byte[] data, CancellationToken ct)
    {
        var tmp = path + ".tmp";
        await File.WriteAllBytesAsync(tmp, data, ct);
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
    }
}
