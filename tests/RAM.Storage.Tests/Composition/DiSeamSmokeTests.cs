using Microsoft.Extensions.DependencyInjection;
using RAM.App;
using RAM.Core.Abstractions;
using RAM.Plugins.Abstractions;

namespace RAM.Storage.Tests.Composition;

public class DiSeamSmokeTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddRamSeams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void All_extension_seams_resolve()
    {
        using var sp = BuildProvider();
        Assert.NotNull(sp.GetRequiredService<IPluginHost>());
        Assert.NotNull(sp.GetRequiredService<IPresenceProvider>());
        Assert.NotNull(sp.GetRequiredService<IRemoteControl>());
        Assert.NotNull(sp.GetRequiredService<IThemeSource>());
        Assert.NotNull(sp.GetRequiredService<ILocalizer>());
        Assert.NotNull(sp.GetRequiredService<IAutoStartService>());
    }

    [Fact]
    public void Plugin_host_starts_with_zero_plugins()
    {
        using var sp = BuildProvider();
        var host = sp.GetRequiredService<IPluginHost>();
        Assert.Empty(host.Plugins);
    }

    [Fact]
    public async Task NoOp_plugin_host_start_stop_does_not_throw()
    {
        using var sp = BuildProvider();
        var host = sp.GetRequiredService<IPluginHost>();
        await host.StartAllAsync();
        await host.StopAllAsync();
    }

    [Fact]
    public async Task NoOp_remote_control_is_inert()
    {
        using var sp = BuildProvider();
        var rc = sp.GetRequiredService<IRemoteControl>();
        Assert.False(rc.IsRunning);
        await rc.StartAsync();
        Assert.False(rc.IsRunning);
        await rc.StopAsync();
    }

    [Fact]
    public async Task NoOp_presence_provider_returns_empty()
    {
        using var sp = BuildProvider();
        var presence = sp.GetRequiredService<IPresenceProvider>();
        var result = await presence.GetPresenceAsync(new ulong[] { 1, 2, 3 });
        Assert.Empty(result);
    }

    [Fact]
    public void NoOp_theme_source_has_default_theme()
    {
        using var sp = BuildProvider();
        var theme = sp.GetRequiredService<IThemeSource>();
        Assert.Equal("default", theme.ActiveThemeName);
        Assert.NotNull(theme.Tokens);
    }

    [Fact]
    public void NoOp_localizer_returns_key()
    {
        using var sp = BuildProvider();
        var loc = sp.GetRequiredService<ILocalizer>();
        Assert.Equal("hello.world", loc["hello.world"]);
        Assert.Equal("en", loc.CurrentCulture);
    }

    [Fact]
    public async Task NoOp_autostart_is_disabled_and_set_is_inert()
    {
        using var sp = BuildProvider();
        var autostart = sp.GetRequiredService<IAutoStartService>();
        Assert.False(await autostart.IsEnabledAsync());
        await autostart.SetEnabledAsync(true);
        Assert.False(await autostart.IsEnabledAsync());
    }

    [Fact]
    public void Seams_are_singletons()
    {
        using var sp = BuildProvider();
        var a = sp.GetRequiredService<IPluginHost>();
        var b = sp.GetRequiredService<IPluginHost>();
        Assert.Same(a, b);
    }
}
