using Microsoft.Extensions.DependencyInjection;
using RAM.Roblox.Api;

namespace RAM.Roblox.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddRobloxAuth(this IServiceCollection services)
    {
        services.AddSingleton<CookieLogin>();
        services.AddSingleton<AuthTicketProvider>();

        services.AddSingleton<PasswordLogin>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(RobloxApi.HttpClientName);
            return new PasswordLogin(
                http,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RobloxApiOptions>>(),
                sp.GetRequiredService<CsrfTokenCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PasswordLogin>>());
        });

        services.AddSingleton<PinUnlockClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(RobloxApi.HttpClientName);
            return new PinUnlockClient(
                http,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RobloxApiOptions>>(),
                sp.GetRequiredService<CsrfTokenCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PinUnlockClient>>());
        });

        return services;
    }
}
