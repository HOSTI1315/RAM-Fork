using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAM.App;
using RAM.App.ViewModels;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Plugins.Abstractions;
using RAM.Roblox;
using RAM.Storage;

namespace RAM.SmokeTests.Scenarios;

internal static class DiHostSmokeTest
{
    public static async Task<string?> RunAsync()
    {
        using var temp = new TempRoot("di-host");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddRamSeams();
        builder.Services.AddRamStorage(rootDirectory: temp.Path, passwordProvider: () => "smoke-pw");
        builder.Services.AddRamRoblox();
        builder.Services.AddRamViewModels();

        using var host = builder.Build();

        // Pre-populate a couple of accounts so LoadAsync has data to verify
        var store = host.Services.GetRequiredService<IAccountStore>();
        await store.SaveAllAsync(new List<Account>
        {
            new() { UserId = 1, Username = "smokeUser1", Cookie = "c1", Group = "01 Test" },
            new() { UserId = 2, Username = "smokeUser2", Cookie = "c2", Group = "01 Test" },
        });

        // Check every seam resolves to its no-op implementation, and behaves as a no-op
        var pluginHost = host.Services.GetRequiredService<IPluginHost>();
        var presence   = host.Services.GetRequiredService<IPresenceProvider>();
        var remote     = host.Services.GetRequiredService<IRemoteControl>();
        var theme      = host.Services.GetRequiredService<IThemeSource>();
        var localizer  = host.Services.GetRequiredService<ILocalizer>();
        var autostart  = host.Services.GetRequiredService<IAutoStartService>();
        var dialogs    = host.Services.GetRequiredService<IDialogService>();
        var files      = host.Services.GetRequiredService<IFileDialogService>();
        var rejoin     = host.Services.GetRequiredService<RAM.Roblox.Rejoin.IRejoinManager>();

        if (pluginHost.Plugins.Count != 0)
            throw new InvalidOperationException("PluginHost should start with 0 plugins");
        if (remote.IsRunning)
            throw new InvalidOperationException("RemoteControl should be inert in v1");
        if (await autostart.IsEnabledAsync())
            throw new InvalidOperationException("AutoStart should default disabled");
        if (await dialogs.ConfirmAsync("t", "m"))
            throw new InvalidOperationException("NoOpDialogService.ConfirmAsync should default false");
        if (await files.SaveFileAsync("title", "JSON|*.json", "f.json") is not null)
            throw new InvalidOperationException("NoOpFileDialogService.SaveFileAsync should default null");
        if (rejoin is null)
            throw new InvalidOperationException("IRejoinManager should resolve");

        // Resolve top-level VMs and load
        var shell = host.Services.GetRequiredService<ShellViewModel>();
        await shell.AccountList.LoadAsync();

        if (shell.AccountList.TotalCount != 2)
            throw new InvalidOperationException(
                $"Expected 2 accounts loaded, got {shell.AccountList.TotalCount}");
        if (shell.AccountList.Groups.Count != 1 || shell.AccountList.Groups[0] != "01 Test")
            throw new InvalidOperationException("Group derivation failed");

        return $"DI graph resolved: 8 seams + IRejoinManager + IAccountStore + ShellViewModel. " +
               $"Loaded {shell.AccountList.TotalCount} accounts, theme={theme.ActiveThemeName}, " +
               $"localizer culture={localizer.CurrentCulture}";
    }
}
