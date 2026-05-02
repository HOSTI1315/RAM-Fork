using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace RAM.Roblox.Rejoin;

[SupportedOSPlatform("windows")]
public static class RejoinServiceCollectionExtensions
{
    /// <summary>
    /// Registers the auto-rejoin <see cref="RejoinManager"/> as a singleton against
    /// <see cref="IRejoinManager"/>. Depends on <c>ILauncher</c>, <c>IPresenceProvider</c>,
    /// <c>IOptions&lt;AppSettings&gt;</c>, and <c>ILoggerFactory</c> already being registered.
    /// </summary>
    public static IServiceCollection AddRobloxRejoin(this IServiceCollection services)
    {
        services.AddSingleton<RejoinManager>();
        services.AddSingleton<IRejoinManager>(sp => sp.GetRequiredService<RejoinManager>());
        return services;
    }
}
