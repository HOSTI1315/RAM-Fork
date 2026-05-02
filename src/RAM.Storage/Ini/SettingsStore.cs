using System.Globalization;
using IniParser;
using IniParser.Model;
using Microsoft.Extensions.Logging;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Storage.Ini;

public sealed class SettingsStore : ISettingsStore
{
    private const string FileName = "RAMSettings.ini";
    private const int SupportedSchemaVersion = 1;

    private readonly IDataDirectoryProvider _data;
    private readonly ILogger<SettingsStore> _logger;

    public SettingsStore(IDataDirectoryProvider data, ILogger<SettingsStore> logger)
    {
        _data = data;
        _logger = logger;
    }

    public string FilePath => Path.Combine(_data.DataDirectory, FileName);

    public Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath))
        {
            _logger.LogInformation("RAMSettings.ini not found, returning defaults");
            return Task.FromResult(new AppSettings());
        }

        var parser = new FileIniDataParser();
        var data = parser.ReadFile(FilePath);

        var schemaVersion = ParseInt(data["meta"]["schema_version"], SupportedSchemaVersion);
        if (schemaVersion > SupportedSchemaVersion)
            throw new InvalidDataException(
                $"RAMSettings.ini schema_version={schemaVersion} is newer than this build supports " +
                $"(max {SupportedSchemaVersion}). Please update RAM.");

        var settings = new AppSettings
        {
            SchemaVersion = schemaVersion,
            MultiInstanceEnabled = ParseBool(data["launcher"]["multi_instance"], true),
            CookieFileLockEnabled = ParseBool(data["launcher"]["cookie_file_lock"], true),
            DefaultProfile = ParseEnum<BotProfile>(data["launcher"]["default_profile"], BotProfile.Normal),
            BackupRetentionHours = ParseInt(data["storage"]["backup_retention_hours"], 8),
            RejoinCheckIntervalSeconds = ParseInt(data["rejoin"]["check_interval_seconds"], 15),
            RejoinGracePeriodSeconds = ParseInt(data["rejoin"]["grace_period_seconds"], 15),
            MemoryThresholdMb = ParseInt(data["watcher"]["memory_threshold_mb"], 200),
            WindowTitleCheckIntervalSeconds = ParseInt(data["watcher"]["title_check_interval_seconds"], 5),
            LogLevel = data["logging"]["level"] ?? "Information",
            DefaultProxy = NullIfEmpty(data["network"]["default_proxy"]),
            PresenceProvider = ParseEnum<PresenceProviderKind>(data["api"]["presence_provider"], PresenceProviderKind.Polling),
        };
        return Task.FromResult(settings);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var data = new IniData();
        data["meta"]["schema_version"] = settings.SchemaVersion.ToString(CultureInfo.InvariantCulture);
        data["launcher"]["multi_instance"] = settings.MultiInstanceEnabled.ToString();
        data["launcher"]["cookie_file_lock"] = settings.CookieFileLockEnabled.ToString();
        data["launcher"]["default_profile"] = settings.DefaultProfile.ToString();
        data["storage"]["backup_retention_hours"] = settings.BackupRetentionHours.ToString(CultureInfo.InvariantCulture);
        data["rejoin"]["check_interval_seconds"] = settings.RejoinCheckIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        data["rejoin"]["grace_period_seconds"] = settings.RejoinGracePeriodSeconds.ToString(CultureInfo.InvariantCulture);
        data["watcher"]["memory_threshold_mb"] = settings.MemoryThresholdMb.ToString(CultureInfo.InvariantCulture);
        data["watcher"]["title_check_interval_seconds"] = settings.WindowTitleCheckIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        data["logging"]["level"] = settings.LogLevel;
        data["network"]["default_proxy"] = settings.DefaultProxy ?? "";
        data["api"]["presence_provider"] = settings.PresenceProvider.ToString();

        var parser = new FileIniDataParser();
        parser.WriteFile(FilePath, data);
        _logger.LogDebug("Wrote RAMSettings.ini");
        return Task.CompletedTask;
    }

    private static int ParseInt(string? value, int @default) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : @default;

    private static bool ParseBool(string? value, bool @default) =>
        bool.TryParse(value, out var b) ? b : @default;

    private static T ParseEnum<T>(string? value, T @default) where T : struct =>
        Enum.TryParse<T>(value, ignoreCase: true, out var v) ? v : @default;

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
