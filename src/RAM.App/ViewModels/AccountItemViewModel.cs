using CommunityToolkit.Mvvm.ComponentModel;
using RAM.Core.Models;

namespace RAM.App.ViewModels;

/// <summary>
/// Per-row view model for the account list. Wraps the underlying <see cref="Account"/>
/// and adds runtime UI state: selection, presence, status badge, thumbnail URL.
/// </summary>
public sealed partial class AccountItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Account account;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private AccountStatus status = AccountStatus.Unknown;

    [ObservableProperty]
    private PresenceType presenceType = PresenceType.Offline;

    [ObservableProperty]
    private string? lastLocation;

    [ObservableProperty]
    private string? thumbnailUrl;

    [ObservableProperty]
    private long robux;

    /// <summary>
    /// Auto-rejoin worker FSM state. Updated by <c>RejoinManager</c> on every
    /// transition. Independent of <see cref="Status"/>; UI may show both.
    /// </summary>
    [ObservableProperty]
    private RejoinWorkerState workerState = RejoinWorkerState.Idle;

    public AccountItemViewModel(Account account)
    {
        this.account = account;
    }

    public ulong UserId => Account.UserId;
    public string Username => Account.Username;
    public string DisplayName => string.IsNullOrEmpty(Account.DisplayName) ? Account.Username : Account.DisplayName;
    public string Group => Account.Group;
    public string Alias => Account.Alias;
    public IReadOnlyList<string> Tags => Account.Tags;
    public bool IsDisabled => Account.Disabled;

    public string DisplayLine => string.IsNullOrEmpty(Alias)
        ? DisplayName
        : $"{Alias} ({DisplayName})";

    partial void OnAccountChanged(Account value)
    {
        OnPropertyChanged(nameof(UserId));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Group));
        OnPropertyChanged(nameof(Alias));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(DisplayLine));
    }
}
