using RAM.App.ViewModels;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

public class AccountListViewModelTests
{
    private sealed class FakeStore : IAccountStore
    {
        public List<Account> Data { get; } = new();
        public Task<IReadOnlyList<Account>> LoadAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Account>>(Data);
        public Task SaveAllAsync(IReadOnlyList<Account> accounts, CancellationToken ct = default)
        {
            Data.Clear(); Data.AddRange(accounts); return Task.CompletedTask;
        }
    }

    private sealed class FakeLauncher : ILauncher
    {
        public int LaunchedCount { get; private set; }
        public Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default)
        {
            LaunchedCount++;
            return Task.FromResult(LaunchResult.Ok(1234, "tracker"));
        }
    }

    private static AccountListViewModel Build(params Account[] seed)
    {
        var store = new FakeStore();
        store.Data.AddRange(seed);
        return new AccountListViewModel(store, new FakeLauncher(), new FakeDialogService(), new FakeRejoinManager());
    }

    [Fact]
    public async Task Load_populates_visible_accounts_and_groups()
    {
        var vm = Build(
            new Account { UserId = 1, Username = "alpha", Cookie = "c", Group = "01 Farming" },
            new Account { UserId = 2, Username = "beta", Cookie = "c", Group = "02 Trading" },
            new Account { UserId = 3, Username = "gamma", Cookie = "c", Group = "01 Farming" });

        await vm.LoadAsync();

        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(3, vm.VisibleCount);
        Assert.Equal(2, vm.Groups.Count);
        Assert.Equal("01 Farming", vm.Groups[0]);
        Assert.Equal("02 Trading", vm.Groups[1]);
    }

    [Fact]
    public async Task SearchText_filters_by_username_alias_or_tag()
    {
        var vm = Build(
            new Account { UserId = 1, Username = "alphabot", Cookie = "c" },
            new Account { UserId = 2, Username = "betafarm", Cookie = "c", Alias = "main" },
            new Account { UserId = 3, Username = "gamma", Cookie = "c", Tags = new[] { "trading" } });
        await vm.LoadAsync();

        vm.SearchText = "bot";
        Assert.Single(vm.VisibleAccounts);
        Assert.Equal("alphabot", vm.VisibleAccounts[0].Username);

        vm.SearchText = "main";
        Assert.Single(vm.VisibleAccounts);
        Assert.Equal("betafarm", vm.VisibleAccounts[0].Username);

        vm.SearchText = "trading";
        Assert.Single(vm.VisibleAccounts);
        Assert.Equal("gamma", vm.VisibleAccounts[0].Username);

        vm.SearchText = "";
        Assert.Equal(3, vm.VisibleAccounts.Count);
    }

    [Fact]
    public async Task SelectedGroup_filters_by_group()
    {
        var vm = Build(
            new Account { UserId = 1, Username = "alpha", Cookie = "c", Group = "Farming" },
            new Account { UserId = 2, Username = "beta", Cookie = "c", Group = "Trading" });
        await vm.LoadAsync();

        vm.SelectedGroup = "Farming";
        Assert.Single(vm.VisibleAccounts);
        Assert.Equal("alpha", vm.VisibleAccounts[0].Username);
    }

    [Fact]
    public async Task Visible_accounts_are_ordered_by_group_numeric_prefix()
    {
        var vm = Build(
            new Account { UserId = 1, Username = "z", Cookie = "c", Group = "10 Late" },
            new Account { UserId = 2, Username = "a", Cookie = "c", Group = "01 Early" },
            new Account { UserId = 3, Username = "m", Cookie = "c", Group = "Other" });
        await vm.LoadAsync();

        Assert.Equal("01 Early", vm.VisibleAccounts[0].Group);
        Assert.Equal("10 Late", vm.VisibleAccounts[1].Group);
        Assert.Equal("Other", vm.VisibleAccounts[2].Group);
    }

    [Fact]
    public async Task Launch_command_calls_launcher_and_updates_status()
    {
        var launcher = new FakeLauncher();
        var store = new FakeStore();
        store.Data.Add(new Account { UserId = 1, Username = "x", Cookie = "c" });
        var vm = new AccountListViewModel(store, launcher, new FakeDialogService(), new FakeRejoinManager());

        await vm.LoadAsync();
        var item = vm.VisibleAccounts[0];
        await vm.LaunchAsync(item);

        Assert.Equal(1, launcher.LaunchedCount);
        Assert.Equal(AccountStatus.NotInGame, item.Status);
    }

    [Fact]
    public async Task Add_inserts_into_visible_and_groups()
    {
        var vm = Build();
        await vm.LoadAsync();
        Assert.Empty(vm.VisibleAccounts);

        vm.Add(new Account { UserId = 1, Username = "new", Cookie = "c", Group = "Group1" });

        Assert.Single(vm.VisibleAccounts);
        Assert.Single(vm.Groups);
    }

    [Fact]
    public async Task Property_change_events_fire_on_search_filter()
    {
        var vm = Build(
            new Account { UserId = 1, Username = "alpha", Cookie = "c" },
            new Account { UserId = 2, Username = "beta", Cookie = "c" });
        await vm.LoadAsync();

        var searchChanged = false;
        var visibleCountChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountListViewModel.SearchText)) searchChanged = true;
            if (e.PropertyName == nameof(AccountListViewModel.VisibleCount)) visibleCountChanged = true;
        };

        vm.SearchText = "alp";

        Assert.True(searchChanged);
        Assert.True(visibleCountChanged);
    }
}
