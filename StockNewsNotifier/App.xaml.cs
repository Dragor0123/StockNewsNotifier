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
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services;
using StockNewsNotifier.Services.Crawlers;
using StockNewsNotifier.Services.Interfaces;

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

            Log.Information("Host started successfully");

            // TEST: Run Yahoo Finance crawler test
            await RunCrawlerTestAsync();

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

    /// <summary>
    /// TEST METHOD: Test Yahoo Finance crawler
    /// </summary>
    private async Task RunCrawlerTestAsync()
    {
        try
        {
            Log.Information("=== Starting Yahoo Finance Crawler Test ===");

            using var scope = _host!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var watchlistService = scope.ServiceProvider.GetRequiredService<IWatchlistService>();
            var newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
            var crawler = scope.ServiceProvider.GetRequiredService<ISourceCrawler>();

            // Step 1: Ensure YahooFinance source exists
            var yahooSource = await db.Sources.FirstOrDefaultAsync(s => s.Name == "YahooFinance");
            if (yahooSource == null)
            {
                yahooSource = new Source
                {
                    Name = "YahooFinance",
                    BaseUrl = "https://finance.yahoo.com",
                    Enabled = true,
                    DisplayName = "Yahoo Finance"
                };
                db.Sources.Add(yahooSource);
                await db.SaveChangesAsync();
                Log.Information("Created YahooFinance source");
            }

            // Step 2: Add MSFT ticker to watchlist
            var ticker = new Ticker("NASDAQ", "MSFT");
            var watchItem = await watchlistService.AddAsync(ticker);
            Log.Information("Added watch item for {Ticker}", ticker);

            // Step 3: Build URL and crawl
            var urls = crawler.BuildQueryUrls(watchItem);
            Log.Information("Crawling URL: {Url}", urls[0]);

            var articles = await crawler.FetchAsync(urls[0], CancellationToken.None);
            Log.Information("Fetched {Count} articles", articles.Count);

            // Step 4: Ingest articles into database
            var newCount = await newsService.IngestAsync(watchItem, yahooSource.Id, articles, CancellationToken.None);
            Log.Information("Ingested {NewCount} new articles", newCount);

            // Step 5: Get all news for MSFT
            var allNews = await newsService.ListAsync(watchItem.Id, days: 7, unreadOnly: false, CancellationToken.None);

            // Step 6: Show results
            var message = $"✅ Yahoo Finance Crawler Test Results:\n\n" +
                         $"🔍 Crawled URL: {urls[0]}\n" +
                         $"📰 Articles found: {articles.Count}\n" +
                         $"✨ New articles added: {newCount}\n" +
                         $"📊 Total articles in DB: {allNews.Count}\n\n" +
                         $"Sample articles:\n";

            var sampleArticles = allNews.Take(5);
            foreach (var article in sampleArticles)
            {
                message += $"\n• {article.Title}\n  ({article.PublishedUtc?.ToString("g") ?? "Unknown time"})\n";
            }

            Log.Information("Test completed successfully");
            MessageBox.Show(message, "Crawler Test Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Crawler test failed");
            MessageBox.Show($"❌ Crawler test failed:\n\n{ex.Message}\n\nCheck logs for details.",
                "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
