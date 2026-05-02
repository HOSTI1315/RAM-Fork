using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAM.App;
using RAM.App.ViewModels;
using RAM.Plugins.Abstractions;
using RAM.Roblox;
using RAM.Storage;
using RAM.Storage.Logging;
using RAM.UI.Services;
using RAM.UI.Views;
using Serilog;

namespace RAM.UI;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureSerilog();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();

        builder.Services.AddRamSeams();
        builder.Services.AddRamStorage();
        builder.Services.AddRamRoblox();
        builder.Services.AddRamViewModels();

        // Override the no-op IDialogService / IFileDialogService from AddRamSeams.
        builder.Services.AddSingleton<IDialogService, WpfDialogService>();
        builder.Services.AddSingleton<IFileDialogService, WpfFileDialogService>();

        _host = builder.Build();
        Log.Logger.Information("App starting up — host built, version {Version}",
            typeof(App).Assembly.GetName().Version?.ToString() ?? "(unknown)");

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
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// Wires Serilog to a daily-rolling file in <c>%AppData%\RAM\logs\</c> with secret
    /// redaction. Without this the default ILogger pipeline writes to console / debug,
    /// which a WPF app has no way to surface — every silent launch failure stays silent.
    /// </summary>
    private static void ConfigureSerilog()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RAM", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.With(new SecretRedactingEnricher())
            .WriteTo.File(
                path: Path.Combine(logDir, "ram-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Logger.Fatal(args.ExceptionObject as Exception, "Unhandled domain exception");
    }
}
