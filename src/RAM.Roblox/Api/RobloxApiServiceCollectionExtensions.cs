using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using RAM.Core.Abstractions;
using RAM.Roblox.Api.Batching;

namespace RAM.Roblox.Api;

public static class RobloxApiServiceCollectionExtensions
{
    public static IServiceCollection AddRobloxApi(this IServiceCollection services)
    {
        services.AddOptions<RobloxApiOptions>();
        services.AddSingleton<CsrfTokenCache>();

        services.AddHttpClient(RobloxApi.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All,
            })
            .AddPolicyHandler(GetRetryPolicy());

        services.AddSingleton<IRobloxApi>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient(RobloxApi.HttpClientName);
            return new RobloxApi(
                http,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RobloxApiOptions>>(),
                sp.GetRequiredService<CsrfTokenCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RobloxApi>>());
        });

        services.AddSingleton<PresenceBatcher>();
        services.AddSingleton<ThumbnailBatcher>();
        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)));
}
