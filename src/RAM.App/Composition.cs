using Microsoft.Extensions.DependencyInjection;
using RAM.App.NoOps;
using RAM.Core.Abstractions;
using RAM.Plugins.Abstractions;

namespace RAM.App;

public static class Composition
{
    /// <summary>
    /// Registers all extension seams as no-op singletons. Real implementations registered
    /// by feature modules in subsequent steps replace these via TryAddSingleton patterns
    /// or explicit overrides.
    /// </summary>
    public static IServiceCollection AddRamSeams(this IServiceCollection services)
    {
        services.AddSingleton<IPluginHost, NoOpPluginHost>();
        services.AddSingleton<IPresenceProvider, NoOpPresenceProvider>();
        services.AddSingleton<IRemoteControl, NoOpRemoteControl>();
        services.AddSingleton<IThemeSource, NoOpThemeSource>();
        services.AddSingleton<ILocalizer, NoOpLocalizer>();
        services.AddSingleton<IAutoStartService, NoOpAutoStartService>();
        services.AddSingleton<IDialogService, NoOpDialogService>();
        services.AddSingleton<IFileDialogService, NoOpFileDialogService>();
        return services;
    }
}
