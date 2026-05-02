using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Plugins.Abstractions;
using RAM.Roblox.Rejoin;

namespace RAM.App.ViewModels;

/// <summary>
/// Top-level VM for the account list view. Owns the master collection, derived filtered
/// view, group sections, and commands the UI binds to (single-account row actions plus
/// bulk multi-select operations).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class AccountListViewModel : ObservableObject
{
    private readonly IAccountStore _store;
    private readonly ILauncher _launcher;
    private readonly IDialogService _dialogs;
    private readonly IRejoinManager _rejoinManager;
    private readonly List<AccountItemViewModel> _all = new();

    public ObservableCollection<AccountItemViewModel> VisibleAccounts { get; } = new();
    public ObservableCollection<string> Groups { get; } = new();

    /// <summary>
    /// Two-way synced with the WPF ListBox via <c>ListBoxSelectedItemsBehavior</c>.
    /// VM is the source of truth; UI mirrors it.
    /// </summary>
    public ObservableCollection<AccountItemViewModel> SelectedItems { get; } = new();

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private string? selectedGroup;

    /// <summary>Most-recently focused single item — drives ShellViewModel.ActiveDetail.</summary>
    [ObservableProperty]
    private AccountItemViewModel? selectedItem;

    [ObservableProperty]
    private bool isLoading;

    public int TotalCount => _all.Count;
    public int VisibleCount => VisibleAccounts.Count;

    public bool IsAllSelected => SelectedGroup is null;
    public bool HasSelection => SelectedItems.Count > 0;
    public bool HasMultiSelection => SelectedItems.Count >= 2;
    public int SelectionCount => SelectedItems.Count;

    /// <summary>True when there is no data at all and we are not currently loading.</summary>
    public bool IsEmpty => !IsLoading && _all.Count == 0;

    /// <summary>True when loading and the in-memory list is still empty (skeleton rows).</summary>
    public bool ShowSkeleton => IsLoading && _all.Count == 0;

    /// <summary>True when the real list should be visible.</summary>
    public bool IsListVisible => _all.Count > 0;

    public AccountListViewModel(
        IAccountStore store,
        ILauncher launcher,
        IDialogService dialogs,
        IRejoinManager rejoinManager)
    {
        _store = store;
        _launcher = launcher;
        _dialogs = dialogs;
        _rejoinManager = rejoinManager;
        SelectedItems.CollectionChanged += (_, _) => OnSelectedItemsChanged();
    }

    private void OnSelectedItemsChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasMultiSelection));
        OnPropertyChanged(nameof(SelectionCount));
        // [RelayCommand] strips "Async" suffix → LaunchSelectedCommand / RefreshSelectedCommand.
        LaunchSelectedCommand.NotifyCanExecuteChanged();
        RefreshSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        MoveSelectedToGroupCommand.NotifyCanExecuteChanged();
        AddTagToSelectedCommand.NotifyCanExecuteChanged();
        ClearSelectionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var accounts = await _store.LoadAllAsync(ct);
            _all.Clear();
            foreach (var a in accounts) _all.Add(new AccountItemViewModel(a));
            RebuildGroups();
            ApplyFilter();
            OnPropertyChanged(nameof(TotalCount));
            RaiseEmptyState();
        }
        finally { IsLoading = false; }
    }

    partial void OnIsLoadingChanged(bool value) => RaiseEmptyState();

    private void RaiseEmptyState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowSkeleton));
        OnPropertyChanged(nameof(IsListVisible));
    }

    [RelayCommand]
    public Task RefreshAsync(CancellationToken ct = default) => LoadAsync(ct);

    [RelayCommand]
    public async Task LaunchAsync(AccountItemViewModel? item, CancellationToken ct = default)
    {
        if (item is null) return;
        await LaunchCustomAsync(item, new LaunchTarget.Place(0), ct);
    }

    /// <summary>Launch with a custom <see cref="LaunchTarget"/> (used by LaunchDialog).</summary>
    public async Task LaunchCustomAsync(
        AccountItemViewModel item,
        LaunchTarget target,
        CancellationToken ct = default)
    {
        item.Status = AccountStatus.Restarting;
        var result = await _launcher.LaunchAsync(new LaunchRequest(item.Account, target), ct);
        item.Status = result.IsSuccess ? AccountStatus.NotInGame : AccountStatus.Error;

        // Hand off to the rejoin manager. The callback is marshalled to the UI thread by
        // the ViewModel — RejoinWorker fires it on its consumer thread, so we round-trip
        // through the WPF dispatcher. The cast keeps RAM.App agnostic of WPF directly:
        // any sync context captured by ConfigureAwait(true) here runs on UI.
        if (result.IsSuccess && !item.IsDisabled)
        {
            _rejoinManager.OnAccountLaunched(
                item.Account,
                result,
                target,
                workerStateChanged: state => OnWorkerStateChanged(item, state));
        }
    }

    private void OnWorkerStateChanged(AccountItemViewModel item, RejoinWorkerState state)
    {
        // Marshal to UI thread via the dispatcher captured at app start (set by
        // ShellViewModel) — fall back to direct assignment if no dispatcher is set
        // (headless tests).
        if (UiDispatcher is { } dispatch)
            dispatch(() => item.WorkerState = state);
        else
            item.WorkerState = state;
    }

    /// <summary>UI dispatcher hook. Set by the WPF host once at startup. Tests leave it null
    /// so callbacks run synchronously on the worker's consumer thread.</summary>
    public static Action<Action>? UiDispatcher { get; set; }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public async Task LaunchSelectedAsync(CancellationToken ct = default)
    {
        var snapshot = SelectedItems.ToList();
        foreach (var item in snapshot)
        {
            if (ct.IsCancellationRequested) break;
            await LaunchAsync(item, ct);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public Task RefreshSelectedAsync(CancellationToken ct = default)
    {
        // Per-account presence/robux refresh comes in Step 8. For now, full reload.
        return LoadAsync(ct);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public async Task DeleteSelectedAsync(CancellationToken ct = default)
    {
        var count = SelectionCount;
        var first = SelectedItems.FirstOrDefault();
        var confirmed = await _dialogs.ConfirmAsync(
            count == 1 ? "Delete account" : "Delete accounts",
            count == 1 && first is not null
                ? $"Permanently delete '{first.DisplayName}'? This cannot be undone."
                : $"Permanently delete {count} accounts? This cannot be undone.",
            new ConfirmDialogOptions(ConfirmText: "Delete", Destructive: true));
        if (!confirmed) return;

        var snapshot = SelectedItems.ToList();
        SelectedItems.Clear();
        foreach (var item in snapshot)
            Remove(item);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public void MoveSelectedToGroup(string? targetGroup)
    {
        if (targetGroup is null) return;
        // TODO Step 7.4: prompt for target group via IDialogService when called with null.
        foreach (var item in SelectedItems)
            item.Account = item.Account with { Group = targetGroup };
        RebuildGroups();
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public void AddTagToSelected(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        // TODO Step 7.4: prompt for tag string via IDialogService when called with null.
        foreach (var item in SelectedItems)
        {
            if (item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) continue;
            var newTags = new List<string>(item.Tags) { tag };
            item.Account = item.Account with { Tags = newTags };
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public void ClearSelection() => SelectedItems.Clear();

    [RelayCommand]
    public void ClearGroup() => SelectedGroup = null;

    /// <summary>Raised when AddAccount command fires. ShellViewModel subscribes and opens
    /// the Add Account overlay dialog. Event-based delegation avoids a DI cycle.</summary>
    public event EventHandler? AddAccountRequested;

    /// <summary>Raised when OpenLaunchDialog fires. ShellViewModel opens the launch overlay.</summary>
    public event EventHandler<AccountItemViewModel?>? LaunchDialogRequested;

    /// <summary>Raised when OpenSettings fires. ShellViewModel opens the settings overlay.</summary>
    public event EventHandler? SettingsRequested;

    [RelayCommand]
    public void AddAccount() => AddAccountRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void OpenLaunchDialog(AccountItemViewModel? item)
        => LaunchDialogRequested?.Invoke(this, item);

    [RelayCommand]
    public void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void Reauth(AccountItemViewModel? item)
    {
        if (item is null) return;
        // TODO Step 7.4: route to AddAccountDialog with pre-filled username + force re-cookie.
    }

    public AccountItemViewModel Add(Account account)
    {
        var vm = new AccountItemViewModel(account);
        _all.Add(vm);
        RebuildGroups();
        ApplyFilter();
        OnPropertyChanged(nameof(TotalCount));
        RaiseEmptyState();
        return vm;
    }

    public void Remove(AccountItemViewModel item)
    {
        _all.Remove(item);
        // Best-effort: tear down any watcher attached to this account. Runs in background;
        // we don't block the UI thread on disposal.
        _ = _rejoinManager.OnAccountRemovedAsync(item.UserId);
        RebuildGroups();
        ApplyFilter();
        OnPropertyChanged(nameof(TotalCount));
        RaiseEmptyState();
    }

    /// <summary>Called by AccountDetailViewModel after a save flips Disabled true.</summary>
    public void NotifyAccountDisabled(ulong userId) => _rejoinManager.OnAccountDisabled(userId);

    /// <summary>App-shutdown hook — called from <c>App.OnExit</c>.</summary>
    public Task ShutdownRejoinAsync(CancellationToken ct = default)
        => _rejoinManager.ShutdownAsync(ct);

    /// <summary>Single-account delete (from row context menu or detail panel).</summary>
    [RelayCommand]
    public async Task DeleteAccountAsync(AccountItemViewModel? item)
    {
        if (item is null) return;
        var confirmed = await _dialogs.ConfirmAsync(
            "Delete account",
            $"Permanently delete '{item.DisplayName}'? This cannot be undone.",
            new ConfirmDialogOptions(ConfirmText: "Delete", Destructive: true));
        if (!confirmed) return;
        SelectedItems.Remove(item);
        Remove(item);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedGroupChanged(string? value)
    {
        OnPropertyChanged(nameof(IsAllSelected));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        VisibleAccounts.Clear();
        var search = (SearchText ?? "").Trim();
        var group = SelectedGroup;

        var filtered = _all.AsEnumerable();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(a =>
                a.Username.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Alias.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }
        if (!string.IsNullOrEmpty(group))
            filtered = filtered.Where(a => a.Group == group);

        var ordered = filtered
            .OrderBy(a => SortKeyOf(a.Group))
            .ThenBy(a => a.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase);

        foreach (var a in ordered) VisibleAccounts.Add(a);
        OnPropertyChanged(nameof(VisibleCount));
    }

    private void RebuildGroups()
    {
        var groupNames = _all
            .Select(a => a.Group)
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SortKeyOf)
            .ThenBy(g => g, StringComparer.OrdinalIgnoreCase);
        Groups.Clear();
        foreach (var g in groupNames) Groups.Add(g);
    }

    private static int SortKeyOf(string? name)
    {
        if (string.IsNullOrEmpty(name)) return int.MaxValue;
        var span = name.AsSpan().TrimStart();
        var i = 0;
        while (i < span.Length && char.IsDigit(span[i])) i++;
        return i > 0 && int.TryParse(span[..i], out var n) ? n : int.MaxValue;
    }
}
