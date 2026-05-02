using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAM.Core.Models;
using RAM.Plugins.Abstractions;
using RAM.Roblox.Auth;
using RAM.Storage.Json;

namespace RAM.App.ViewModels;

/// <summary>
/// 3-tab Add Account dialog VM: Cookie / Username·Password / Bulk Import. The
/// Pass tab promotes itself into a 2FA challenge sub-state when the API returns
/// <see cref="LoginResult.TwoFactorRequired"/> — single VM, single dialog window.
/// </summary>
public sealed partial class AddAccountViewModel : ObservableObject
{
    private readonly CookieLogin _cookieLogin;
    private readonly PasswordLogin _passwordLogin;
    private readonly Importer? _importer;
    private readonly IFileDialogService? _fileDialog;
    private readonly Action<IReadOnlyList<Account>> _close;
    private ulong _twoFactorUserId;
    private string _twoFactorTicket = string.Empty;
    private IReadOnlyList<Account> _bulkPreviewAccounts = Array.Empty<Account>();

    public IReadOnlyList<string> AvailableGroups { get; }

    [ObservableProperty] private int selectedTabIndex;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    // Cookie tab
    [ObservableProperty] private string cookieValue = string.Empty;
    [ObservableProperty] private string alias = string.Empty;
    [ObservableProperty] private string group = string.Empty;

    // Username · Password tab
    [ObservableProperty] private string username = string.Empty;
    [ObservableProperty] private string password = string.Empty;

    // 2FA sub-state (within Pass tab)
    [ObservableProperty] private bool twoFactorVisible;
    [ObservableProperty] private string twoFactorCode = string.Empty;
    [ObservableProperty] private TwoFactorMediaType twoFactorMediaType = TwoFactorMediaType.Email;

    public IReadOnlyList<TwoFactorMediaType> AvailableTwoFactorMediaTypes { get; } =
        new[] { TwoFactorMediaType.Email, TwoFactorMediaType.Authenticator };

    // Bulk tab
    [ObservableProperty] private string bulkCookies = string.Empty;
    [ObservableProperty] private int bulkValidatedCount;
    [ObservableProperty] private int bulkFailedCount;
    [ObservableProperty] private bool bulkInProgress;

    // File-import preview (populated when user drops/picks a .json file)
    public ObservableCollection<string> BulkFilePreviewNames { get; } = new();
    [ObservableProperty] private int bulkFilePreviewCount;
    [ObservableProperty] private string? bulkFileImportPath;
    [ObservableProperty] private string? bulkFileImportWarning;

    public bool BulkFilePreviewVisible => BulkFilePreviewCount > 0;

    public AddAccountViewModel(
        CookieLogin cookieLogin,
        PasswordLogin passwordLogin,
        Action<IReadOnlyList<Account>> close,
        IReadOnlyList<string> availableGroups,
        Importer? importer = null,
        IFileDialogService? fileDialog = null)
    {
        _cookieLogin = cookieLogin;
        _passwordLogin = passwordLogin;
        _importer = importer;
        _fileDialog = fileDialog;
        _close = close;
        AvailableGroups = availableGroups;
    }

    [RelayCommand]
    public async Task PickBulkFileAsync()
    {
        if (_fileDialog is null) return;
        var path = await _fileDialog.OpenFileAsync(
            "Import accounts from JSON",
            "JSON files (*.json)|*.json|All files (*.*)|*.*");
        if (path is not null) await LoadBulkFileAsync(path);
    }

    public async Task LoadBulkFileAsync(string path, CancellationToken ct = default)
    {
        if (_importer is null) return;
        try
        {
            var result = await _importer.ImportAsync(path, ct);
            _bulkPreviewAccounts = result.Accounts;
            BulkFilePreviewNames.Clear();
            foreach (var a in result.Accounts.Take(20)) BulkFilePreviewNames.Add(a.Username);
            BulkFilePreviewCount = result.Accounts.Count;
            BulkFileImportPath = path;
            BulkFileImportWarning = result.Warnings.Count > 0 ? string.Join(" · ", result.Warnings) : null;
            ErrorMessage = null;
            OnPropertyChanged(nameof(BulkFilePreviewVisible));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Import failed: {ex.Message}";
            ClearBulkPreview();
        }
    }

    private void ClearBulkPreview()
    {
        _bulkPreviewAccounts = Array.Empty<Account>();
        BulkFilePreviewNames.Clear();
        BulkFilePreviewCount = 0;
        BulkFileImportPath = null;
        BulkFileImportWarning = null;
        OnPropertyChanged(nameof(BulkFilePreviewVisible));
    }

    [RelayCommand]
    public void Cancel() => _close(Array.Empty<Account>());

    [RelayCommand]
    public async Task SubmitCookieAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(CookieValue))
        {
            ErrorMessage = "Paste a .ROBLOSECURITY cookie above.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _cookieLogin.ValidateAsync(CookieValue.Trim(), ct);
            FinishWith(result);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cookie validation failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task SubmitPasswordAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _passwordLogin.StartAsync(Username, Password, ct);
            switch (result)
            {
                case LoginResult.TwoFactorRequired tfr:
                    _twoFactorUserId = tfr.UserId;
                    _twoFactorTicket = tfr.ChallengeId;
                    TwoFactorMediaType = tfr.MediaType;
                    TwoFactorCode = string.Empty;
                    TwoFactorVisible = true;
                    break;
                default:
                    FinishWith(result);
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task SubmitTwoFactorAsync(CancellationToken ct = default)
    {
        if (TwoFactorCode.Length < 4)
        {
            ErrorMessage = "Enter the verification code.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _passwordLogin.CompleteTwoFactorAsync(
                _twoFactorUserId, TwoFactorMediaType, _twoFactorTicket, TwoFactorCode.Trim(), ct);
            FinishWith(result);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"2FA verification failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task SubmitBulkAsync(CancellationToken ct = default)
    {
        // If a file preview is loaded, prefer that over the textarea.
        if (BulkFilePreviewVisible)
        {
            if (!string.IsNullOrEmpty(Group))
            {
                var withGroup = _bulkPreviewAccounts
                    .Select(a => a with { Group = Group })
                    .ToList();
                _close(withGroup);
            }
            else _close(_bulkPreviewAccounts);
            return;
        }

        if (string.IsNullOrWhiteSpace(BulkCookies))
        {
            ErrorMessage = "Paste cookies (one per line) or drop a .json file.";
            return;
        }

        var lines = BulkCookies
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.StartsWith("_|WARNING", StringComparison.Ordinal) ||
                        l.Length > 100)
            .Distinct()
            .ToList();

        BulkInProgress = true;
        BulkValidatedCount = 0;
        BulkFailedCount = 0;
        ErrorMessage = null;

        var accounts = new List<Account>();
        try
        {
            foreach (var cookie in lines)
            {
                if (ct.IsCancellationRequested) break;
                var result = await _cookieLogin.ValidateAsync(cookie, ct);
                if (result is LoginResult.Success s)
                {
                    accounts.Add(new Account
                    {
                        UserId = s.UserId,
                        Username = s.Username,
                        DisplayName = s.DisplayName,
                        Cookie = s.Cookie,
                        Group = Group,
                    });
                    BulkValidatedCount++;
                }
                else
                {
                    BulkFailedCount++;
                }
            }
            if (accounts.Count > 0) _close(accounts);
            else ErrorMessage = "No valid cookies were imported.";
        }
        finally { BulkInProgress = false; }
    }

    private void FinishWith(LoginResult result)
    {
        switch (result)
        {
            case LoginResult.Success s:
                var account = new Account
                {
                    UserId = s.UserId,
                    Username = s.Username,
                    DisplayName = s.DisplayName,
                    Cookie = s.Cookie,
                    Alias = Alias,
                    Group = Group,
                };
                _close(new[] { account });
                break;
            case LoginResult.Failed f:
                ErrorMessage = f.Reason;
                break;
        }
    }
}
