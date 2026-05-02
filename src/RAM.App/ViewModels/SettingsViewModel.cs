using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Plugins.Abstractions;
using RAM.Storage.Json;

namespace RAM.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IAccountStore? _accountStore;
    private readonly Exporter? _exporter;
    private readonly IFileDialogService? _fileDialog;
    private readonly IDialogService? _dialogs;
    private AppSettings _original = new();

    [ObservableProperty] private bool multiInstanceEnabled = true;
    [ObservableProperty] private bool cookieFileLockEnabled = true;
    [ObservableProperty] private BotProfile defaultProfile = BotProfile.Normal;
    [ObservableProperty] private int backupRetentionHours = 8;
    [ObservableProperty] private int rejoinCheckIntervalSeconds = 15;
    [ObservableProperty] private int rejoinGracePeriodSeconds = 15;
    [ObservableProperty] private int memoryThresholdMb = 200;
    [ObservableProperty] private int windowTitleCheckIntervalSeconds = 5;
    [ObservableProperty] private string logLevel = "Information";
    [ObservableProperty] private string defaultProxy = "";
    [ObservableProperty] private PresenceProviderKind presenceProvider = PresenceProviderKind.Polling;
    [ObservableProperty] private bool isDirty;

    public IReadOnlyList<BotProfile> AvailableProfiles { get; } =
        new[] { BotProfile.Normal, BotProfile.BottingPlayer, BotProfile.BottingBot };

    public IReadOnlyList<string> AvailableLogLevels { get; } =
        new[] { "Trace", "Debug", "Information", "Warning", "Error" };

    public IReadOnlyList<PresenceProviderKind> AvailablePresenceProviders { get; } =
        new[] { PresenceProviderKind.Polling, PresenceProviderKind.WebSocket };

    public SettingsViewModel(
        ISettingsStore store,
        IAccountStore? accountStore = null,
        Exporter? exporter = null,
        IFileDialogService? fileDialog = null,
        IDialogService? dialogs = null)
    {
        _store = store;
        _accountStore = accountStore;
        _exporter = exporter;
        _fileDialog = fileDialog;
        _dialogs = dialogs;
    }

    [RelayCommand]
    public async Task ExportAsync(CancellationToken ct = default)
    {
        if (_accountStore is null || _exporter is null || _fileDialog is null) return;

        var path = await _fileDialog.SaveFileAsync(
            "Export accounts (plaintext)",
            "JSON files (*.json)|*.json",
            $"ram-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        if (path is null) return;

        // Optional confirm — exporting plaintext cookies is risky.
        if (_dialogs is not null)
        {
            var ok = await _dialogs.ConfirmAsync(
                "Export plaintext?",
                "Cookies will be written in cleartext to the chosen file. Anyone with " +
                "access to the file can sign in as these accounts. Continue?",
                new ConfirmDialogOptions(ConfirmText: "Export", Destructive: true));
            if (!ok) return;
        }

        var accounts = await _accountStore.LoadAllAsync(ct);
        await _exporter.SaveAsync(accounts, path, ct);
    }

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var s = await _store.LoadAsync(ct);
        ApplyFromSettings(s);
    }

    [RelayCommand]
    public async Task SaveAsync(CancellationToken ct = default)
    {
        var s = ToSettings();
        await _store.SaveAsync(s, ct);
        _original = s;
        IsDirty = false;
    }

    [RelayCommand]
    public void Cancel() => ApplyFromSettings(_original);

    public AppSettings ToSettings() => new()
    {
        SchemaVersion = _original.SchemaVersion,
        MultiInstanceEnabled = MultiInstanceEnabled,
        CookieFileLockEnabled = CookieFileLockEnabled,
        DefaultProfile = DefaultProfile,
        BackupRetentionHours = BackupRetentionHours,
        RejoinCheckIntervalSeconds = RejoinCheckIntervalSeconds,
        RejoinGracePeriodSeconds = RejoinGracePeriodSeconds,
        MemoryThresholdMb = MemoryThresholdMb,
        WindowTitleCheckIntervalSeconds = WindowTitleCheckIntervalSeconds,
        LogLevel = LogLevel,
        DefaultProxy = string.IsNullOrEmpty(DefaultProxy) ? null : DefaultProxy,
        PresenceProvider = PresenceProvider,
    };

    private void ApplyFromSettings(AppSettings s)
    {
        _original = s;
        MultiInstanceEnabled = s.MultiInstanceEnabled;
        CookieFileLockEnabled = s.CookieFileLockEnabled;
        DefaultProfile = s.DefaultProfile;
        BackupRetentionHours = s.BackupRetentionHours;
        RejoinCheckIntervalSeconds = s.RejoinCheckIntervalSeconds;
        RejoinGracePeriodSeconds = s.RejoinGracePeriodSeconds;
        MemoryThresholdMb = s.MemoryThresholdMb;
        WindowTitleCheckIntervalSeconds = s.WindowTitleCheckIntervalSeconds;
        LogLevel = s.LogLevel;
        DefaultProxy = s.DefaultProxy ?? "";
        PresenceProvider = s.PresenceProvider;
        IsDirty = false;
    }

    partial void OnMultiInstanceEnabledChanged(bool value) => MarkDirty();
    partial void OnCookieFileLockEnabledChanged(bool value) => MarkDirty();
    partial void OnDefaultProfileChanged(BotProfile value) => MarkDirty();
    partial void OnBackupRetentionHoursChanged(int value) => MarkDirty();
    partial void OnRejoinCheckIntervalSecondsChanged(int value) => MarkDirty();
    partial void OnRejoinGracePeriodSecondsChanged(int value) => MarkDirty();
    partial void OnMemoryThresholdMbChanged(int value) => MarkDirty();
    partial void OnWindowTitleCheckIntervalSecondsChanged(int value) => MarkDirty();
    partial void OnLogLevelChanged(string value) => MarkDirty();
    partial void OnDefaultProxyChanged(string value) => MarkDirty();
    partial void OnPresenceProviderChanged(PresenceProviderKind value) => MarkDirty();

    private void MarkDirty()
    {
        if (!IsDirty) IsDirty = true;
    }
}
