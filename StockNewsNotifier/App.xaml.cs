using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StockNewsNotifier.Data;

namespace StockNewsNotifier;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure Serilog before building the host
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(GetAppDataFolder(), "Logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("Starting StockNewsNotifier application");

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Add appsettings.json from the application directory
                    var appDir = AppContext.BaseDirectory;
                    config.AddJsonFile(Path.Combine(appDir, "appsettings.json"), optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // Database
                    var dbPath = GetDatabasePath();
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseSqlite($"Data Source={dbPath}"));

                    // Services will be added in Phase 2
                    // For now, just register the DbContext

                    // TODO: Add services in Phase 2
                    // services.AddSingleton<INotificationService, WindowsToastNotificationService>();
                    // services.AddSingleton<IScheduler, ChannelScheduler>();
                    // services.AddScoped<IWatchlistService, WatchlistService>();
                    // services.AddScoped<INewsService, NewsService>();

                    // HttpClient for crawlers (will be used in Phase 3)
                    // services.AddHttpClient("crawler");

                    // TODO: Add crawlers in Phase 3
                    // services.AddSingleton<ISourceCrawler, YahooFinanceCrawler>();

                    // TODO: Add background service in Phase 5
                    // services.AddHostedService<NewsPollerHostedService>();

                    // TODO: Add ViewModels in Phase 6
                    // services.AddSingleton<MainViewModel>();
                })
                .Build();

            await _host.StartAsync();

            Log.Information("Host started successfully");

            // TODO: Show main window or tray icon in Phase 6
            // For now, we'll just let the host run
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
            MessageBox.Show($"Application failed to start: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("Shutting down StockNewsNotifier application");

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static string GetAppDataFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "StockNewsNotifier");
        Directory.CreateDirectory(appFolder);
        return appFolder;
    }

    private static string GetDatabasePath()
    {
        var appFolder = GetAppDataFolder();
        return Path.Combine(appFolder, "news.db");
    }
}
