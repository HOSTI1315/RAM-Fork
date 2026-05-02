using RAM.App.ViewModels;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

public class ShellViewModelTests
{
    private sealed class FakeStore : IAccountStore
    {
        public List<Account> Data { get; } = new();
        public Task<IReadOnlyList<Account>> LoadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Account>>(Data);
        public Task SaveAllAsync(IReadOnlyList<Account> accounts, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(new AppSettings());
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeLauncher : ILauncher
    {
        public Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default)
            => Task.FromResult(LaunchResult.Ok(1, "t"));
    }

    [Fact]
    public async Task Selecting_account_creates_active_detail_view_model()
    {
        var store = new FakeStore();
        store.Data.Add(new Account { UserId = 1, Username = "u", Cookie = "c", DisplayName = "User" });
        var list = new AccountListViewModel(store, new FakeLauncher(), new FakeDialogService(), new FakeRejoinManager());
        var settings = new SettingsViewModel(new FakeSettingsStore());
        var shell = new ShellViewModel(list, settings);

        await list.LoadAsync();
        Assert.Null(shell.ActiveDetail);

        list.SelectedItem = list.VisibleAccounts[0];

        Assert.NotNull(shell.ActiveDetail);
        Assert.Equal("User", shell.ActiveDetail!.DisplayName);
    }

    [Fact]
    public void Title_defaults_set()
    {
        var list = new AccountListViewModel(new FakeStore(), new FakeLauncher(), new FakeDialogService(), new FakeRejoinManager());
        var settings = new SettingsViewModel(new FakeSettingsStore());
        var shell = new ShellViewModel(list, settings);
        Assert.Equal("Roblox Account Manager", shell.Title);
    }
}
