using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAM.App;
using RAM.App.ViewModels;
using RAM.Plugins.Abstractions;
using RAM.Roblox;
using RAM.Storage;
using RAM.UI.Services;
using RAM.UI.Views;

namespace RAM.UI;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddRamSeams();
        builder.Services.AddRamStorage();
        builder.Services.AddRamRoblox();
        builder.Services.AddRamViewModels();

        // Override the no-op IDialogService / IFileDialogService from AddRamSeams.
        builder.Services.AddSingleton<IDialogService, WpfDialogService>();
        builder.Services.AddSingleton<IFileDialogService, WpfFileDialogService>();

        _host = builder.Build();

        // Hook the WPF dispatcher so RejoinWorker callbacks (which run on a non-UI thread)
        // marshal back to UI before mutating ObservableProperty-backed VM state.
        AccountListViewModel.UiDispatcher = action => Dispatcher.InvokeAsync(action);

        var shellVm = _host.Services.GetRequiredService<ShellViewModel>();
        var window = new ShellView { DataContext = shellVm };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Stop all auto-rejoin workers gracefully before tearing down DI.
        if (_host is not null)
        {
            try
            {
                var list = _host.Services.GetService<AccountListViewModel>();
                list?.ShutdownRejoinAsync().GetAwaiter().GetResult();
            }
            catch { /* best-effort during shutdown */ }
        }
        _host?.Dispose();
        base.OnExit(e);
    }
}
