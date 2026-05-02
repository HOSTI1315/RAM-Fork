using RAM.App.ViewModels;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

public class AccountListMultiSelectTests
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
        public List<Account> Launched { get; } = new();
        public Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default)
        {
            Launched.Add(request.Account);
            return Task.FromResult(LaunchResult.Ok(1, "tracker"));
        }
    }

    private static (AccountListViewModel vm, FakeStore store, FakeLauncher launcher, FakeDialogService dialogs) Build(params Account[] seed)
    {
        var store = new FakeStore();
        store.Data.AddRange(seed);
        var launcher = new FakeLauncher();
        var dialogs = new FakeDialogService();
        return (new AccountListViewModel(store, launcher, dialogs, new FakeRejoinManager()), store, launcher, dialogs);
    }

    [Fact]
    public async Task HasSelection_flips_when_SelectedItems_populated()
    {
        var (vm, _, _, _) = Build(new Account { UserId = 1, Username = "a", Cookie = "c" });
        await vm.LoadAsync();

        Assert.False(vm.HasSelection);
        Assert.False(vm.HasMultiSelection);
        Assert.Equal(0, vm.SelectionCount);

        vm.SelectedItems.Add(vm.VisibleAccounts[0]);

        Assert.True(vm.HasSelection);
        Assert.False(vm.HasMultiSelection);
        Assert.Equal(1, vm.SelectionCount);
    }

    [Fact]
    public async Task HasMultiSelection_requires_at_least_two()
    {
        var (vm, _, _, _) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c" },
            new Account { UserId = 2, Username = "b", Cookie = "c" },
            new Account { UserId = 3, Username = "d", Cookie = "c" });
        await vm.LoadAsync();

        vm.SelectedItems.Add(vm.VisibleAccounts[0]);
        Assert.False(vm.HasMultiSelection);

        vm.SelectedItems.Add(vm.VisibleAccounts[1]);
        Assert.True(vm.HasMultiSelection);
    }

    [Fact]
    public async Task LaunchSelected_iterates_all_selected()
    {
        var (vm, _, launcher, _) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c1" },
            new Account { UserId = 2, Username = "b", Cookie = "c2" },
            new Account { UserId = 3, Username = "d", Cookie = "c3" });
        await vm.LoadAsync();

        vm.SelectedItems.Add(vm.VisibleAccounts[0]);
        vm.SelectedItems.Add(vm.VisibleAccounts[2]);
        // Bulk Launch needs a sidebar Place ID — without one each LaunchAsync would
        // route to the LaunchDialog (which is the v1.1 bug-fix behaviour).
        vm.LaunchPlaceId = "606849621";

        await vm.LaunchSelectedAsync();

        Assert.Equal(2, launcher.Launched.Count);
        Assert.Contains(launcher.Launched, a => a.UserId == 1);
        Assert.Contains(launcher.Launched, a => a.UserId == 3);
    }

    [Fact]
    public async Task LaunchSelectedCommand_disabled_when_no_selection()
    {
        var (vm, _, _, _) = Build(new Account { UserId = 1, Username = "a", Cookie = "c" });
        await vm.LoadAsync();
        Assert.False(vm.LaunchSelectedCommand.CanExecute(null));

        vm.SelectedItems.Add(vm.VisibleAccounts[0]);
        Assert.True(vm.LaunchSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteSelected_removes_from_store_and_clears_selection_when_confirmed()
    {
        var (vm, _, _, dialogs) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c" },
            new Account { UserId = 2, Username = "b", Cookie = "c" },
            new Account { UserId = 3, Username = "d", Cookie = "c" });
        await vm.LoadAsync();

        vm.SelectedItems.Add(vm.VisibleAccounts[0]);
        vm.SelectedItems.Add(vm.VisibleAccounts[1]);
        dialogs.ConfirmResult = true;

        await vm.DeleteSelectedAsync();

        Assert.Equal(1, vm.TotalCount);
        Assert.Empty(vm.SelectedItems);
        Assert.Equal(3ul, vm.VisibleAccounts[0].UserId);
        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.True(dialogs.LastConfirmOptions?.Destructive);
    }

    [Fact]
    public async Task DeleteSelected_skips_deletion_when_confirm_cancelled()
    {
        var (vm, _, _, dialogs) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c" },
            new Account { UserId = 2, Username = "b", Cookie = "c" });
        await vm.LoadAsync();
        vm.SelectedItems.Add(vm.VisibleAccounts[0]);
        dialogs.ConfirmResult = false;

        await vm.DeleteSelectedAsync();

        Assert.Equal(2, vm.TotalCount);
        Assert.Single(vm.SelectedItems);
    }

    [Fact]
    public async Task MoveSelectedToGroup_updates_account_group()
    {
        var (vm, _, _, _) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c", Group = "Old" },
            new Account { UserId = 2, Username = "b", Cookie = "c", Group = "Old" });
        await vm.LoadAsync();

        vm.SelectedItems.Add(vm.VisibleAccounts[0]);
        vm.SelectedItems.Add(vm.VisibleAccounts[1]);

        vm.MoveSelectedToGroup("New");

        Assert.All(vm.VisibleAccounts, a => Assert.Equal("New", a.Group));
        Assert.Contains("New", vm.Groups);
    }

    [Fact]
    public async Task MoveSelectedToGroup_with_null_is_noop()
    {
        var (vm, _, _, _) = Build(new Account { UserId = 1, Username = "a", Cookie = "c", Group = "G" });
        await vm.LoadAsync();
        vm.SelectedItems.Add(vm.VisibleAccounts[0]);

        vm.MoveSelectedToGroup(null);

        Assert.Equal("G", vm.VisibleAccounts[0].Group);
    }

    [Fact]
    public async Task AddTagToSelected_adds_unique_tag()
    {
        var (vm, _, _, _) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c", Tags = new[] { "alt" } },
            new Account { UserId = 2, Username = "b", Cookie = "c" });
        await vm.LoadAsync();

        vm.SelectedItems.Add(vm.VisibleAccounts[0]);
        vm.SelectedItems.Add(vm.VisibleAccounts[1]);

        vm.AddTagToSelected("vip");

        Assert.Contains("vip", vm.VisibleAccounts[0].Tags);
        Assert.Contains("alt", vm.VisibleAccounts[0].Tags);
        Assert.Contains("vip", vm.VisibleAccounts[1].Tags);
    }

    [Fact]
    public async Task AddTagToSelected_skips_existing_tag()
    {
        var (vm, _, _, _) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c", Tags = new[] { "vip" } });
        await vm.LoadAsync();
        vm.SelectedItems.Add(vm.VisibleAccounts[0]);

        vm.AddTagToSelected("VIP"); // case-insensitive — should not duplicate

        Assert.Single(vm.VisibleAccounts[0].Tags);
    }

    [Fact]
    public async Task ClearSelection_empties_collection()
    {
        var (vm, _, _, _) = Build(new Account { UserId = 1, Username = "a", Cookie = "c" });
        await vm.LoadAsync();
        vm.SelectedItems.Add(vm.VisibleAccounts[0]);

        vm.ClearSelection();

        Assert.Empty(vm.SelectedItems);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public async Task ClearGroup_resets_SelectedGroup_to_null()
    {
        var (vm, _, _, _) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c", Group = "G1" });
        await vm.LoadAsync();
        vm.SelectedGroup = "G1";
        Assert.False(vm.IsAllSelected);

        vm.ClearGroup();

        Assert.Null(vm.SelectedGroup);
        Assert.True(vm.IsAllSelected);
    }

    [Fact]
    public async Task IsAllSelected_property_change_event_fires()
    {
        var (vm, _, _, _) = Build(
            new Account { UserId = 1, Username = "a", Cookie = "c", Group = "G1" });
        await vm.LoadAsync();

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedGroup = "G1";

        Assert.Contains(nameof(AccountListViewModel.IsAllSelected), raised);
    }

    [Fact]
    public async Task Reauth_command_with_null_is_noop()
    {
        var (vm, _, _, _) = Build();
        await vm.LoadAsync();
        // Should not throw
        vm.Reauth(null);
        await Task.Yield();
    }

    [Fact]
    public void AddAccount_command_resolves_and_is_executable()
    {
        var (vm, _, _, _) = Build();
        Assert.True(vm.AddAccountCommand.CanExecute(null));
        vm.AddAccountCommand.Execute(null); // placeholder; doesn't throw
    }

    [Fact]
    public async Task Reauth_command_with_item_does_not_throw()
    {
        var (vm, _, _, _) = Build(new Account { UserId = 1, Username = "a", Cookie = "c" });
        await vm.LoadAsync();
        vm.Reauth(vm.VisibleAccounts[0]);
        await Task.Yield();
    }
}
