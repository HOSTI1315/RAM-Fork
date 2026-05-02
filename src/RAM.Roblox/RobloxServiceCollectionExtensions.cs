using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using RAM.Roblox.Api;
using RAM.Roblox.Auth;
using RAM.Roblox.Launch;
using RAM.Roblox.Rejoin;

namespace RAM.Roblox;

[SupportedOSPlatform("windows")]
public static class RobloxServiceCollectionExtensions
{
    /// <summary>
    /// Umbrella registration: API + Auth + Launcher + Rejoin. Configure
    /// <see cref="RobloxApiOptions"/> separately if non-default endpoints are needed.
    /// </summary>
    public static IServiceCollection AddRamRoblox(this IServiceCollection services)
    {
        services.AddRobloxApi();
        services.AddRobloxAuth();
        services.AddRobloxLauncher();
        services.AddRobloxRejoin();
        return services;
    }
}
