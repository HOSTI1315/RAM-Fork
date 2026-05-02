using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Plugins.Abstractions;
using RAM.Roblox.Auth;
using RAM.Storage.Json;

namespace RAM.App.ViewModels;

/// <summary>
/// Top-level shell VM the WPF window will bind to. Holds child VMs and selected page.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly CookieLogin? _cookieLogin;
    private readonly PasswordLogin? _passwordLogin;
    private readonly Importer? _importer;
    private readonly IFileDialogService? _fileDialog;

    [ObservableProperty]
    private string title = "Roblox Account Manager";

    public AccountListViewModel AccountList { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private AccountDetailViewModel? activeDetail;

    /// <summary>
    /// Window-overlay dialog. ShellView renders a semi-transparent backdrop + the
    /// matching DataTemplate when this is non-null. Cancel / Close routes back here
    /// via the dialog's callback (set in OpenXxxAsync helpers).
    /// </summary>
    [ObservableProperty]
    private ObservableObject? activeDialog;

    public ShellViewModel(
        AccountListViewModel accountList,
        SettingsViewModel settings,
        CookieLogin? cookieLogin = null,
        PasswordLogin? passwordLogin = null,
        Importer? importer = null,
        IFileDialogService? fileDialog = null)
    {
        AccountList = accountList;
        Settings = settings;
        _cookieLogin = cookieLogin;
        _passwordLogin = passwordLogin;
        _importer = importer;
        _fileDialog = fileDialog;
        AccountList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(AccountListViewModel.SelectedItem)) return;
            var item = AccountList.SelectedItem;
            ActiveDetail = item is null ? null : BuildDetailVm(item);
        };

        // Wire AccountList event-style command delegates to ShellVM dialogs. Avoids a
        // DI cycle (Shell→AccountList→Shell) while keeping AccountList ignorant of UI.
        AccountList.AddAccountRequested      += (_, _) => OpenAddAccountCommand.Execute(null);
        AccountList.LaunchDialogRequested    += (_, item) => OpenLaunchDialogCommand.Execute(item);
        AccountList.SettingsRequested        += (_, _) => OpenSettingsCommand.Execute(null);
    }

    /// <summary>Build a detail VM bound to <paramref name="item"/>. The save callback
    /// captures the row so detail edits propagate back into the list and persist to disk.</summary>
    private AccountDetailViewModel BuildDetailVm(AccountItemViewModel item)
        => new(item.Account, async updated =>
        {
            // Mirror the updated record into the row VM (drives UI refresh of name,
            // group, alias, etc.) and into the master list for persistence.
            item.Account = updated;
            // Notify the rejoin manager if the account was just disabled — it stops
            // the worker but keeps the cached worker ready for a future relaunch.
            if (updated.Disabled) AccountList.NotifyAccountDisabled(updated.UserId);
            await AccountList.PersistAsync();
        });

    /// <summary>Closes the detail panel by clearing the underlying selection.</summary>
    [RelayCommand]
    public void CloseDetail()
    {
        AccountList.SelectedItem = null;
        AccountList.SelectedItems.Clear();
    }

    /// <summary>Closes any active overlay dialog. Bound to backdrop click + Esc.</summary>
    [RelayCommand]
    public void CloseDialog() => ActiveDialog = null;

    /// <summary>Open Add Account dialog (Cookie / Password+2FA / Bulk Import).</summary>
    [RelayCommand]
    public void OpenAddAccount()
    {
        if (_cookieLogin is null || _passwordLogin is null) return; // headless / test mode
        var vm = new AddAccountViewModel(
            _cookieLogin,
            _passwordLogin,
            OnAddAccountClosed,
            AccountList.Groups.ToList(),
            _importer,
            _fileDialog);
        ActiveDialog = vm;
    }

    private void OnAddAccountClosed(IReadOnlyList<Account> accounts)
    {
        ActiveDialog = null;
        foreach (var a in accounts) AccountList.Add(a);
    }

    /// <summary>Open Launch dialog (Place / Job / Follow / Private).</summary>
    [RelayCommand]
    public void OpenLaunchDialog(AccountItemViewModel? item)
    {
        var target = item ?? AccountList.SelectedItem;
        var vm = new LaunchDialogViewModel(target, OnLaunchDialogClosed);
        ActiveDialog = vm;
    }

    private async void OnLaunchDialogClosed(LaunchRequest? request)
    {
        ActiveDialog = null;
        if (request is null) return;
        // Route through AccountList — uses the same single-launch flow as the row-level
        // Launch button. The launch target is custom rather than the default Place(0).
        var item = AccountList.VisibleAccounts.FirstOrDefault(v => v.UserId == request.Account.UserId);
        if (item is null) return;
        await AccountList.LaunchCustomAsync(item, request.Target);
    }

    /// <summary>Open Settings dialog. Reuses the existing <see cref="Settings"/> VM.</summary>
    [RelayCommand]
    public async Task OpenSettingsAsync()
    {
        await Settings.LoadAsync();
        ActiveDialog = Settings;
    }
}
