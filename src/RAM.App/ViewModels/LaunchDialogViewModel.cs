using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAM.Core.Abstractions;
using RAM.Roblox.Launch;

namespace RAM.App.ViewModels;

/// <summary>
/// Launch dialog with 4 mode tabs: Place / Job / Follow / Private.
/// Submit produces a <see cref="LaunchTarget"/> + invokes the parent's Launch flow
/// via the close callback.
/// </summary>
public sealed partial class LaunchDialogViewModel : ObservableObject
{
    private readonly AccountItemViewModel? _account;
    private readonly Action<LaunchRequest?> _close;

    [ObservableProperty] private int selectedTabIndex;
    [ObservableProperty] private string? errorMessage;

    // Tab 0: Place
    [ObservableProperty] private string placeId = string.Empty;

    // Tab 1: Job (Place + Job ID)
    [ObservableProperty] private string jobPlaceId = string.Empty;
    [ObservableProperty] private string jobId = string.Empty;

    // Tab 2: Follow user
    [ObservableProperty] private string followUserId = string.Empty;

    // Tab 3: Private server
    [ObservableProperty] private string privatePlaceId = string.Empty;
    [ObservableProperty] private string privateLinkCode = string.Empty;
    [ObservableProperty] private string privateLinkUrl = string.Empty;

    public string AccountLabel => _account?.DisplayLine ?? "(no account)";

    public LaunchDialogViewModel(
        AccountItemViewModel? account,
        Action<LaunchRequest?> close,
        int initialTabIndex = 0)
    {
        _account = account;
        _close = close;
        SelectedTabIndex = initialTabIndex;
    }

    [RelayCommand]
    public void Cancel() => _close(null);

    [RelayCommand]
    public void Submit()
    {
        if (_account is null)
        {
            ErrorMessage = "No account selected.";
            return;
        }
        ErrorMessage = null;

        LaunchTarget? target = SelectedTabIndex switch
        {
            0 => TryParseUlong(PlaceId) is { } pid ? new LaunchTarget.Place(pid) : null,
            1 => TryParseUlong(JobPlaceId) is { } pid && !string.IsNullOrWhiteSpace(JobId)
                    ? new LaunchTarget.Place(pid, JobId.Trim())
                    : null,
            2 => TryParseUlong(FollowUserId) is { } uid ? new LaunchTarget.FollowUser(uid) : null,
            3 => TryBuildPrivate(),
            _ => null,
        };

        if (target is null)
        {
            ErrorMessage = SelectedTabIndex switch
            {
                0 => "Enter a numeric Place ID.",
                1 => "Enter both Place ID and Job ID.",
                2 => "Enter the user's numeric User ID.",
                3 => "Provide a private-server link or place ID + access code.",
                _ => "Invalid input.",
            };
            return;
        }

        _close(new LaunchRequest(_account.Account, target));
    }

    private LaunchTarget? TryBuildPrivate()
    {
        // Accept either a full share-URL OR raw placeId + linkCode.
        if (!string.IsNullOrWhiteSpace(PrivateLinkUrl))
        {
            var code = PrivateServerLinkParser.TryExtractCode(PrivateLinkUrl);
            if (code is null) return null;
            // Place ID may not be derivable from the share link; require user to also fill it.
            if (TryParseUlong(PrivatePlaceId) is { } pid)
                return new LaunchTarget.PrivateServer(pid, code);
            return null;
        }
        if (TryParseUlong(PrivatePlaceId) is { } placeId &&
            !string.IsNullOrWhiteSpace(PrivateLinkCode))
            return new LaunchTarget.PrivateServer(placeId, PrivateLinkCode.Trim());
        return null;
    }

    private static ulong? TryParseUlong(string s) =>
        ulong.TryParse(s.Trim(), out var n) && n > 0 ? n : null;
}
