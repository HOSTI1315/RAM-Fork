using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
                // Auth endpoints depend on the ORIGINAL response headers (csrf token on 403,
                // auth ticket on 200). Auto-redirect would swap them out for the redirect
                // target's headers and we'd lose the values. We handle 403→retry manually.
                AllowAutoRedirect = false,
            })
            .ConfigureHttpClient((sp, client) =>
            {
                // Apply User-Agent here so RobloxApiOptions.UserAgent actually reaches the
                // wire (a previous wiring gap left it ignored). Roblox's WAF blocks /
                // throttles requests with the default .NET UA.
                var opts = sp.GetRequiredService<IOptions<RobloxApiOptions>>().Value;
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", opts.UserAgent);
            })
            .AddPolicyHandler(GetRetryPolicy());

        services.AddSingleton<IRobloxApi>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient(RobloxApi.HttpClientName);
            return new RobloxApi(
                http,
                sp.GetRequiredService<IOptions<RobloxApiOptions>>(),
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
