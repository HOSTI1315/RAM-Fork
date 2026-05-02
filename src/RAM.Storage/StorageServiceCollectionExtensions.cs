using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RAM.Core.Abstractions;
using RAM.Storage.Crypto;
using RAM.Storage.Ini;
using RAM.Storage.Json;
using RAM.Storage.Logging;
using Serilog.Core;

namespace RAM.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers the encrypted account store, INI settings store, crypto primitives,
    /// and the secret-redacting Serilog enricher.
    /// </summary>
    /// <param name="rootDirectory">
    /// Override the default <c>%LocalAppData%\RAM</c> root (used by tests).
    /// </param>
    /// <param name="passwordProvider">
    /// Returns the master password used for AES envelope encryption. In production this
    /// should prompt the user; tests pass a fixed value.
    /// </param>
    public static IServiceCollection AddRamStorage(
        this IServiceCollection services,
        string? rootDirectory = null,
        Func<string>? passwordProvider = null)
    {
        var resolvedRoot = rootDirectory ?? DefaultRoot();
        services.AddSingleton<IDataDirectoryProvider>(_ => new RamDataDirectory(resolvedRoot));
        services.AddSingleton<SodiumArgon2idCipher>();
        services.AddSingleton<Argon2iLegacyReader>();
        services.AddSingleton<DpapiFallback>();
        services.AddSingleton<ILogEventEnricher, SecretRedactingEnricher>();

        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<Json.Importer>();
        services.AddSingleton<Json.Exporter>();
        services.AddSingleton<IAccountStore>(sp =>
            new AccountStore(
                sp.GetRequiredService<IDataDirectoryProvider>(),
                sp.GetRequiredService<SodiumArgon2idCipher>(),
                sp.GetRequiredService<Argon2iLegacyReader>(),
                sp.GetRequiredService<DpapiFallback>(),
                passwordProvider ?? DefaultPassword,
                sp.GetRequiredService<ILogger<AccountStore>>()));
        return services;
    }

    public static string DefaultRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RAM");

    private static string DefaultPassword() =>
        // Production wires this to an interactive prompt or DPAPI-derived secret.
        "ram-default-dev-password";
}
