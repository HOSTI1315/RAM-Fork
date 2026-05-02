using RAM.App.ViewModels;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

public class ShellCloseDetailTests
{
    private sealed class FakeStore : IAccountStore
    {
        public List<Account> Data { get; } = new();
        public Task<IReadOnlyList<Account>> LoadAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Account>>(Data);
        public Task SaveAllAsync(IReadOnlyList<Account> accounts, CancellationToken ct = default)
            => Task.CompletedTask;
    }
    private sealed class FakeLauncher : ILauncher
    {
        public Task<LaunchResult> LaunchAsync(LaunchRequest r, CancellationToken ct = default)
            => Task.FromResult(LaunchResult.Ok(1, "t"));
    }
    private sealed class FakeSettings : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(new AppSettings());
        public Task SaveAsync(AppSettings s, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task CloseDetail_clears_selection_and_active_detail()
    {
        var store = new FakeStore();
        store.Data.Add(new Account { UserId = 1, Username = "u", Cookie = "c" });

        var list = new AccountListViewModel(store, new FakeLauncher(), new FakeDialogService(), new FakeRejoinManager());
        var settings = new SettingsViewModel(new FakeSettings());
        var shell = new ShellViewModel(list, settings);

        await list.LoadAsync();
        list.SelectedItem = list.VisibleAccounts[0];
        list.SelectedItems.Add(list.VisibleAccounts[0]);
        Assert.NotNull(shell.ActiveDetail);
        Assert.True(list.HasSelection);

        shell.CloseDetailCommand.Execute(null);

        Assert.Null(list.SelectedItem);
        Assert.Empty(list.SelectedItems);
        Assert.Null(shell.ActiveDetail);
    }
}
