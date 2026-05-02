using RAM.App.ViewModels;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

public class AccountDetailViewModelTests
{
    [Fact]
    public void Initial_state_is_clean()
    {
        var vm = new AccountDetailViewModel(new Account
        {
            UserId = 1, Username = "u", Cookie = "c", DisplayName = "U", Alias = "ali", Group = "G",
            Tags = ["a", "b"], Description = "desc",
        });
        Assert.False(vm.IsDirty);
        Assert.Equal("U", vm.DisplayName);
        Assert.Equal("ali", vm.Alias);
        Assert.Equal("a, b", vm.TagsCsv);
    }

    [Fact]
    public void Editing_a_field_marks_dirty()
    {
        var vm = new AccountDetailViewModel(new Account
        {
            UserId = 1, Username = "u", Cookie = "c", DisplayName = "U",
        });
        vm.DisplayName = "Updated";
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void ApplyChanges_returns_updated_record_and_clears_dirty()
    {
        var vm = new AccountDetailViewModel(new Account
        {
            UserId = 1, Username = "u", Cookie = "c", DisplayName = "U",
        });
        vm.DisplayName = "NewName";
        vm.TagsCsv = "x, y, z";

        var updated = vm.ApplyChanges();

        Assert.Equal("NewName", updated.DisplayName);
        Assert.Equal(new[] { "x", "y", "z" }, updated.Tags);
        Assert.Equal(1ul, updated.UserId);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void Cancel_reverts_pending_edits()
    {
        var original = new Account
        {
            UserId = 1, Username = "u", Cookie = "c", DisplayName = "Original", Alias = "OG",
        };
        var vm = new AccountDetailViewModel(original);
        vm.DisplayName = "Changed";
        vm.Alias = "NewAlias";
        Assert.True(vm.IsDirty);

        vm.Cancel();

        Assert.Equal("Original", vm.DisplayName);
        Assert.Equal("OG", vm.Alias);
        Assert.False(vm.IsDirty);
    }
}
