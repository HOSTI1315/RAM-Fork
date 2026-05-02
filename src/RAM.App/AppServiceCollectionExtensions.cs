using Microsoft.Extensions.DependencyInjection;
using RAM.App.ViewModels;

namespace RAM.App;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddRamViewModels(this IServiceCollection services)
    {
        services.AddSingleton<AccountListViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<AccountDetailViewModel>();
        return services;
    }
}
