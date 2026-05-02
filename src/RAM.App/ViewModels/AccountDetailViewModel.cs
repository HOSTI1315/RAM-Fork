using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAM.Core.Models;

namespace RAM.App.ViewModels;

/// <summary>
/// Edits a single account. Tracks dirty state so the UI can prompt before discarding
/// unsaved changes. Save/Cancel commands wire to the parent list.
/// </summary>
public sealed partial class AccountDetailViewModel : ObservableObject
{
    private Account _original;
    private readonly Func<Account, Task>? _saveCallback;

    [ObservableProperty] private string username = "";
    [ObservableProperty] private string displayName = "";
    [ObservableProperty] private string group = "";
    [ObservableProperty] private string alias = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string tagsCsv = "";
    [ObservableProperty] private bool disabled;
    [ObservableProperty] private bool isDirty;

    // ===== Proxy section (schema v2) =====
    [ObservableProperty] private bool proxyEnabled;
    [ObservableProperty] private ProxyType proxyType = ProxyType.Http;
    [ObservableProperty] private string proxyHost = "";
    [ObservableProperty] private int proxyPort = 8080;
    [ObservableProperty] private string proxyUsername = "";
    [ObservableProperty] private string proxyPassword = "";

    public IReadOnlyList<ProxyType> AvailableProxyTypes { get; } =
        new[] { ProxyType.Http, ProxyType.Socks4, ProxyType.Socks5 };

    public ulong UserId => _original.UserId;
    public string Cookie => _original.Cookie;
    public IReadOnlyDictionary<string, string> Fields => _original.Fields;
    public WindowPlacement? WindowPlacement => _original.WindowPlacement;
    public DateTimeOffset Created => _original.Created;
    public DateTimeOffset? LastUsed => _original.LastUsed;

    public AccountDetailViewModel(Account account, Func<Account, Task>? saveCallback = null)
    {
        _original = account;
        _saveCallback = saveCallback;
        ResetFromAccount(account);
    }

    public void ResetFromAccount(Account account)
    {
        _original = account;
        Username = account.Username;
        DisplayName = account.DisplayName;
        Group = account.Group;
        Alias = account.Alias;
        Description = account.Description;
        TagsCsv = string.Join(", ", account.Tags);
        Disabled = account.Disabled;

        // Proxy section
        if (account.Proxy is { } p)
        {
            ProxyEnabled = true;
            ProxyType = p.Type;
            ProxyHost = p.Host;
            ProxyPort = p.Port;
            ProxyUsername = p.Username ?? "";
            ProxyPassword = p.Password ?? "";
        }
        else
        {
            ProxyEnabled = false;
            ProxyType = ProxyType.Http;
            ProxyHost = "";
            ProxyPort = 8080;
            ProxyUsername = "";
            ProxyPassword = "";
        }

        IsDirty = false;
    }

    public Account ApplyChanges()
    {
        var tags = TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var proxy = ProxyEnabled && !string.IsNullOrWhiteSpace(ProxyHost)
            ? new ProxyConfig(
                ProxyType,
                ProxyHost.Trim(),
                ProxyPort,
                string.IsNullOrEmpty(ProxyUsername) ? null : ProxyUsername,
                string.IsNullOrEmpty(ProxyPassword) ? null : ProxyPassword)
            : null;

        var updated = _original with
        {
            DisplayName = DisplayName,
            Group = Group,
            Alias = Alias,
            Description = Description,
            Tags = tags,
            Disabled = Disabled,
            Proxy = proxy,
        };
        _original = updated;
        IsDirty = false;
        return updated;
    }

    [RelayCommand]
    public void Cancel() => ResetFromAccount(_original);

    /// <summary>Persist current edits via the save callback the parent passed in.
    /// Without a callback (test / headless), calling this is equivalent to
    /// <see cref="ApplyChanges"/>.</summary>
    [RelayCommand]
    public async Task SaveAsync()
    {
        if (!IsDirty) return;
        var updated = ApplyChanges();
        if (_saveCallback is not null)
            await _saveCallback(updated);
    }

    partial void OnDisplayNameChanged(string value) => MarkDirty();
    partial void OnGroupChanged(string value) => MarkDirty();
    partial void OnAliasChanged(string value) => MarkDirty();
    partial void OnDescriptionChanged(string value) => MarkDirty();
    partial void OnTagsCsvChanged(string value) => MarkDirty();
    partial void OnDisabledChanged(bool value) => MarkDirty();
    partial void OnProxyEnabledChanged(bool value) => MarkDirty();
    partial void OnProxyTypeChanged(ProxyType value) => MarkDirty();
    partial void OnProxyHostChanged(string value) => MarkDirty();
    partial void OnProxyPortChanged(int value) => MarkDirty();
    partial void OnProxyUsernameChanged(string value) => MarkDirty();
    partial void OnProxyPasswordChanged(string value) => MarkDirty();

    private void MarkDirty()
    {
        if (!IsDirty) IsDirty = true;
    }
}
