namespace RAM.Core.Models;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;

    public bool MultiInstanceEnabled { get; init; } = true;
    public bool CookieFileLockEnabled { get; init; } = true;
    public BotProfile DefaultProfile { get; init; } = BotProfile.Normal;

    public int BackupRetentionHours { get; init; } = 8;
    public int RejoinCheckIntervalSeconds { get; init; } = 15;
    public int RejoinGracePeriodSeconds { get; init; } = 15;
    public int MemoryThresholdMb { get; init; } = 200;
    public int WindowTitleCheckIntervalSeconds { get; init; } = 5;

    public string LogLevel { get; init; } = "Information";
    public string? DefaultProxy { get; init; }

    public PresenceProviderKind PresenceProvider { get; init; } = PresenceProviderKind.Polling;
}

public enum PresenceProviderKind
{
    Polling = 0,
    WebSocket = 1,
}
