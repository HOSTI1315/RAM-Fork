using Microsoft.Extensions.DependencyInjection;
using RAM.Core.Abstractions;
using RAM.Roblox.ClientSettings;
using RAM.Roblox.Watchers;

namespace RAM.Roblox.Launch;

public static class LaunchServiceCollectionExtensions
{
    public static IServiceCollection AddRobloxLauncher(this IServiceCollection services)
    {
        services.AddSingleton<SingletonMutexBypass>();
        services.AddSingleton<CookieFileLock>();
        services.AddSingleton<RobloxInstallLocator>();
        services.AddSingleton<ClientAppSettingsPatcher>();
        services.AddSingleton<ILauncher, RobloxLauncher>();
        return services;
    }
}
