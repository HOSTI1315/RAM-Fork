using RAM.App.ViewModels;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

public class AccountListEmptyStateTests
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

    private static (AccountListViewModel vm, FakeDialogService dialogs) Build(params Account[] seed)
    {
        var store = new FakeStore();
        store.Data.AddRange(seed);
        var dialogs = new FakeDialogService();
        return (new AccountListViewModel(store, new FakeLauncher(), dialogs, new FakeRejoinManager()), dialogs);
    }

    [Fact]
    public async Task After_load_with_zero_accounts_IsEmpty_true()
    {
        var (vm, _) = Build();
        await vm.LoadAsync();
        Assert.True(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
        Assert.False(vm.ShowSkeleton);
    }

    [Fact]
    public async Task After_load_with_accounts_IsListVisible_true()
    {
        var (vm, _) = Build(new Account { UserId = 1, Username = "u", Cookie = "c" });
        await vm.LoadAsync();
        Assert.False(vm.IsEmpty);
        Assert.True(vm.IsListVisible);
        Assert.False(vm.ShowSkeleton);
    }

    [Fact]
    public void IsEmpty_property_change_event_fires_when_loading_completes()
    {
        var (vm, _) = Build();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsLoading = true;
        Assert.Contains(nameof(AccountListViewModel.IsEmpty), raised);
        Assert.Contains(nameof(AccountListViewModel.ShowSkeleton), raised);

        raised.Clear();
        vm.IsLoading = false;
        Assert.Contains(nameof(AccountListViewModel.IsEmpty), raised);
    }

    [Fact]
    public async Task DeleteAccount_with_null_is_noop()
    {
        var (vm, _) = Build(new Account { UserId = 1, Username = "u", Cookie = "c" });
        await vm.LoadAsync();
        await vm.DeleteAccountAsync(null);
        Assert.Equal(1, vm.TotalCount);
    }

    [Fact]
    public async Task DeleteAccount_removes_single_item_when_confirmed()
    {
        var (vm, dialogs) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c" },
            new Account { UserId = 2, Username = "b", Cookie = "c" });
        await vm.LoadAsync();
        var item = vm.VisibleAccounts[0];
        vm.SelectedItems.Add(item);
        dialogs.ConfirmResult = true;

        await vm.DeleteAccountAsync(item);

        Assert.Equal(1, vm.TotalCount);
        Assert.Empty(vm.SelectedItems);
        Assert.Equal(2ul, vm.VisibleAccounts[0].UserId);
    }

    [Fact]
    public async Task DeleteAccount_does_nothing_when_cancelled()
    {
        var (vm, dialogs) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c" });
        await vm.LoadAsync();
        var item = vm.VisibleAccounts[0];
        dialogs.ConfirmResult = false;

        await vm.DeleteAccountAsync(item);

        Assert.Equal(1, vm.TotalCount);
    }

    [Fact]
    public async Task Removing_last_account_flips_IsEmpty_back_to_true()
    {
        var (vm, dialogs) = Build(new Account { UserId = 1, Username = "a", Cookie = "c" });
        await vm.LoadAsync();
        Assert.False(vm.IsEmpty);
        dialogs.ConfirmResult = true;

        await vm.DeleteAccountAsync(vm.VisibleAccounts[0]);

        Assert.True(vm.IsEmpty);
    }
}
