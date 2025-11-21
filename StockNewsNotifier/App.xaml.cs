using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StockNewsNotifier.BackgroundServices;
using StockNewsNotifier.Data;
using StockNewsNotifier.Services;
using StockNewsNotifier.Services.Crawlers;
using StockNewsNotifier.Services.Interfaces;

namespace StockNewsNotifier;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private MainWindow? _mainWindow;
    public static IServiceProvider? Services { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
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

                    services.AddScoped<IWatchlistService, WatchlistService>();
                    services.AddScoped<INewsService, NewsService>();

                    // HttpClient for crawlers
                    services.AddHttpClient("crawler", client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(15);
                        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
                        client.DefaultRequestHeaders.ConnectionClose = false;
                    })
                    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                    {
                        AutomaticDecompression = DecompressionMethods.All,
                        AllowAutoRedirect = true,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
                    });

                    services.AddSingleton<ChannelScheduler>();
                    services.AddSingleton<IScheduler>(sp => sp.GetRequiredService<ChannelScheduler>());

                    // Background infrastructure
                    services.AddSingleton<INotificationService, NotificationService>();
                    services.AddHostedService<NewsPollerHostedService>();

                    // Crawlers
                    services.AddSingleton<ISourceCrawler, YahooFinanceCrawler>();
                })
                .Build();

            await _host.StartAsync();
            Services = _host.Services;

            Log.Information("Host started successfully");

            await SourceSeeder.EnsureDefaultsAsync(Services);

            _mainWindow = new MainWindow();
            _mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
            System.Windows.MessageBox.Show($"Application failed to start: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

        _mainWindow?.DisposeTrayIcon();
        _mainWindow = null;
        Services = null;

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
