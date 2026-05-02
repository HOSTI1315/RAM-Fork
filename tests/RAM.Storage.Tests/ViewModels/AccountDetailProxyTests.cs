using RAM.App.ViewModels;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

public class AccountDetailProxyTests
{
    [Fact]
    public void ResetFromAccount_with_null_proxy_disables_section()
    {
        var vm = new AccountDetailViewModel(
            new Account { UserId = 1, Username = "u", Cookie = "c" });
        Assert.False(vm.ProxyEnabled);
        Assert.Equal(8080, vm.ProxyPort);
        Assert.Equal("", vm.ProxyHost);
    }

    [Fact]
    public void ResetFromAccount_with_populated_proxy_pulls_in_fields()
    {
        var vm = new AccountDetailViewModel(new Account
        {
            UserId = 1, Username = "u", Cookie = "c",
            Proxy = new ProxyConfig(ProxyType.Socks5, "h", 1080, "user", "pw"),
        });
        Assert.True(vm.ProxyEnabled);
        Assert.Equal(ProxyType.Socks5, vm.ProxyType);
        Assert.Equal("h", vm.ProxyHost);
        Assert.Equal(1080, vm.ProxyPort);
        Assert.Equal("user", vm.ProxyUsername);
        Assert.Equal("pw", vm.ProxyPassword);
    }

    [Fact]
    public void ApplyChanges_with_ProxyEnabled_builds_ProxyConfig()
    {
        var vm = new AccountDetailViewModel(
            new Account { UserId = 1, Username = "u", Cookie = "c" });
        vm.ProxyEnabled = true;
        vm.ProxyType = ProxyType.Http;
        vm.ProxyHost = "proxy.local";
        vm.ProxyPort = 3128;
        vm.ProxyUsername = "alice";
        vm.ProxyPassword = "secret";

        var updated = vm.ApplyChanges();

        Assert.NotNull(updated.Proxy);
        Assert.Equal(ProxyType.Http, updated.Proxy!.Type);
        Assert.Equal("proxy.local", updated.Proxy.Host);
        Assert.Equal(3128, updated.Proxy.Port);
        Assert.Equal("alice", updated.Proxy.Username);
        Assert.Equal("secret", updated.Proxy.Password);
    }

    [Fact]
    public void ApplyChanges_without_ProxyEnabled_emits_null_Proxy()
    {
        var vm = new AccountDetailViewModel(new Account
        {
            UserId = 1, Username = "u", Cookie = "c",
            Proxy = new ProxyConfig(ProxyType.Http, "old", 8080),
        });
        vm.ProxyEnabled = false;

        var updated = vm.ApplyChanges();

        Assert.Null(updated.Proxy);
    }

    [Fact]
    public void ApplyChanges_with_ProxyEnabled_but_empty_host_emits_null()
    {
        var vm = new AccountDetailViewModel(
            new Account { UserId = 1, Username = "u", Cookie = "c" });
        vm.ProxyEnabled = true;
        vm.ProxyHost = "  ";

        var updated = vm.ApplyChanges();

        Assert.Null(updated.Proxy);
    }

    [Fact]
    public void ApplyChanges_with_empty_username_emits_null_credentials()
    {
        var vm = new AccountDetailViewModel(
            new Account { UserId = 1, Username = "u", Cookie = "c" });
        vm.ProxyEnabled = true;
        vm.ProxyHost = "h";
        vm.ProxyPort = 1080;
        vm.ProxyType = ProxyType.Socks5;
        // No username/password

        var updated = vm.ApplyChanges();

        Assert.NotNull(updated.Proxy);
        Assert.Null(updated.Proxy!.Username);
        Assert.Null(updated.Proxy.Password);
    }

    [Fact]
    public void Editing_proxy_field_marks_dirty()
    {
        var vm = new AccountDetailViewModel(
            new Account { UserId = 1, Username = "u", Cookie = "c" });
        Assert.False(vm.IsDirty);

        vm.ProxyEnabled = true;
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Cancel_restores_proxy_state_from_original()
    {
        var vm = new AccountDetailViewModel(new Account
        {
            UserId = 1, Username = "u", Cookie = "c",
            Proxy = new ProxyConfig(ProxyType.Http, "orig.proxy", 8080, "u", "pw"),
        });

        vm.ProxyHost = "changed.proxy";
        vm.ProxyPassword = "new-pw";
        Assert.True(vm.IsDirty);

        vm.Cancel();

        Assert.Equal("orig.proxy", vm.ProxyHost);
        Assert.Equal("pw", vm.ProxyPassword);
        Assert.False(vm.IsDirty);
    }
}
