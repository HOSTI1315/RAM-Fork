using RAM.App.ViewModels;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

/// <summary>
/// Regression tests for the v1.0 bug where mutation paths (Add, Remove, MoveSelectedToGroup,
/// AddTagToSelected, DeleteSelectedAsync, AccountDetailViewModel.Save) never called
/// <see cref="IAccountStore.SaveAllAsync"/>. Adding any account, deleting any account,
/// or editing any field was an in-memory-only change — restarting the app lost everything.
///
/// Each test asserts that the corresponding mutation drives at least one persistence
/// write, AND that the persisted snapshot reflects the mutation.
/// </summary>
public class AccountListPersistenceTests
{
    private sealed class CountingStore : IAccountStore
    {
        public List<Account> Data { get; } = new();
        public int SaveCalls { get; private set; }

        public Task<IReadOnlyList<Account>> LoadAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Account>>(Data);

        public Task SaveAllAsync(IReadOnlyList<Account> accounts, CancellationToken ct = default)
        {
            SaveCalls++;
            Data.Clear();
            Data.AddRange(accounts);
            return Task.CompletedTask;
        }
    }

    private sealed class StubLauncher : ILauncher
    {
        public Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default)
            => Task.FromResult(LaunchResult.Ok(1, "t"));
    }

    private static (AccountListViewModel Vm, CountingStore Store) Build(params Account[] seed)
    {
        var store = new CountingStore();
        store.Data.AddRange(seed);
        var vm = new AccountListViewModel(
            store, new StubLauncher(), new FakeDialogService(), new FakeRejoinManager());
        return (vm, store);
    }

    private static Account A(ulong id, string name = "u", string group = "") => new()
    {
        UserId = id, Username = name, Cookie = "c", Group = group,
    };

    // ---- Add ------------------------------------------------------------

    [Fact]
    public async Task Add_persists_account_to_store()
    {
        var (vm, store) = Build();
        await vm.LoadAsync();
        Assert.Equal(0, store.SaveCalls);

        vm.Add(A(1));
        await vm.PersistAsync();   // flush pending fire-and-forget save

        Assert.True(store.SaveCalls >= 1, "SaveAllAsync should have been called after Add");
        Assert.Single(store.Data);
        Assert.Equal(1ul, store.Data[0].UserId);
    }

    // ---- Remove ---------------------------------------------------------

    [Fact]
    public async Task Remove_persists_deletion_to_store()
    {
        var (vm, store) = Build(A(1), A(2));
        await vm.LoadAsync();
        var initialSaves = store.SaveCalls;
        var item = vm.VisibleAccounts.First(v => v.UserId == 1);

        vm.Remove(item);
        await vm.PersistAsync();

        Assert.True(store.SaveCalls > initialSaves, "Remove must trigger SaveAllAsync");
        Assert.Single(store.Data);
        Assert.Equal(2ul, store.Data[0].UserId);
    }

    // ---- DeleteSelectedAsync (batch) ----------------------------------

    [Fact]
    public async Task DeleteSelectedAsync_persists_once_for_batch()
    {
        var (vm, store) = Build(A(1), A(2), A(3));
        await vm.LoadAsync();
        var savesBefore = store.SaveCalls;

        // Select first two and delete (FakeDialogService.ConfirmResult defaults to true).
        vm.SelectedItems.Add(vm.VisibleAccounts.First(v => v.UserId == 1));
        vm.SelectedItems.Add(vm.VisibleAccounts.First(v => v.UserId == 2));
        await vm.DeleteSelectedAsync();

        Assert.True(store.SaveCalls > savesBefore, "DeleteSelectedAsync must trigger save");
        Assert.Single(store.Data);
        Assert.Equal(3ul, store.Data[0].UserId);
    }

    // ---- MoveSelectedToGroup --------------------------------------------

    [Fact]
    public async Task MoveSelectedToGroup_persists_group_change()
    {
        var (vm, store) = Build(A(1, group: ""), A(2, group: ""));
        await vm.LoadAsync();
        vm.SelectedItems.Add(vm.VisibleAccounts.First(v => v.UserId == 1));
        var savesBefore = store.SaveCalls;

        vm.MoveSelectedToGroup("Farming");
        await vm.PersistAsync();

        Assert.True(store.SaveCalls > savesBefore);
        Assert.Equal("Farming", store.Data.Single(a => a.UserId == 1).Group);
    }

    // ---- AddTagToSelected -----------------------------------------------

    [Fact]
    public async Task AddTagToSelected_persists_new_tag()
    {
        var (vm, store) = Build(A(1));
        await vm.LoadAsync();
        vm.SelectedItems.Add(vm.VisibleAccounts.First());
        var savesBefore = store.SaveCalls;

        vm.AddTagToSelected("trader");
        await vm.PersistAsync();

        Assert.True(store.SaveCalls > savesBefore);
        Assert.Contains("trader", store.Data.Single().Tags);
    }

    // ---- AccountDetailViewModel.SaveAsync -------------------------------

    [Fact]
    public async Task DetailVm_Save_invokes_callback_with_updated_account()
    {
        Account? captured = null;
        var detail = new AccountDetailViewModel(
            A(42, name: "old"),
            updated => { captured = updated; return Task.CompletedTask; });

        detail.DisplayName = "new-name";
        Assert.True(detail.IsDirty);

        await detail.SaveAsync();

        Assert.NotNull(captured);
        Assert.Equal("new-name", captured!.DisplayName);
        Assert.False(detail.IsDirty);
    }

    [Fact]
    public async Task DetailVm_Save_no_op_when_not_dirty()
    {
        var calls = 0;
        var detail = new AccountDetailViewModel(
            A(42),
            _ => { calls++; return Task.CompletedTask; });

        await detail.SaveAsync();

        Assert.Equal(0, calls);
    }

    // ---- LaunchAsync — no longer fires placeId=0 ------------------------

    [Fact]
    public async Task LaunchAsync_with_no_sidebar_input_opens_dialog_not_fake_launch()
    {
        var (vm, _) = Build(A(1));
        await vm.LoadAsync();
        var item = vm.VisibleAccounts.First();

        var dialogRequested = false;
        vm.LaunchDialogRequested += (_, _) => dialogRequested = true;

        await vm.LaunchAsync(item);

        Assert.True(dialogRequested,
            "LaunchAsync with empty sidebar Place ID must open the LaunchDialog, " +
            "not silently call ILauncher.LaunchAsync with placeId=0");
    }

    [Fact]
    public async Task LaunchAsync_with_sidebar_placeId_uses_it()
    {
        var (vm, _) = Build(A(1));
        await vm.LoadAsync();
        vm.LaunchPlaceId = "606849621";
        var item = vm.VisibleAccounts.First();

        var dialogRequested = false;
        vm.LaunchDialogRequested += (_, _) => dialogRequested = true;

        await vm.LaunchAsync(item);

        Assert.False(dialogRequested,
            "Sidebar Place ID is set — LaunchAsync should launch directly, not open the dialog");
        // FakeLauncher returns Ok(1, "t"); item should reflect successful launch attempt.
        Assert.Equal(AccountStatus.NotInGame, item.Status);
    }
}
