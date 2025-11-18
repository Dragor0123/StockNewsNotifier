# DESIGN_PLAN.md - Implementation Guide for StockNewsNotifier

## Project Overview
You are building a **Windows System Tray Application** that provides real-time stock news notifications. This is a C# .NET 8 WPF application that crawls news from multiple sources and alerts users about their watched stock tickers.

## Architecture Summary
- **Platform**: .NET 8, WPF (UI) + WinForms NotifyIcon (tray)
- **Database**: EF Core + SQLite
- **Web Scraping**: HttpClient + AngleSharp
- **Resilience**: Polly (retry/backoff)
- **Logging**: Serilog
- **Notifications**: Windows Toast (Microsoft.Toolkit.Uwp.Notifications)

## Project Structure
```
StockNewsNotifier/
├── StockNewsNotifier.sln
├── src/
│   └── StockNewsNotifier/
│       ├── StockNewsNotifier.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── appsettings.json
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   ├── Entities/
│       │   │   ├── WatchItem.cs
│       │   │   ├── Source.cs
│       │   │   ├── WatchItemSource.cs
│       │   │   ├── NewsItem.cs
│       │   │   └── CrawlState.cs
│       │   └── Migrations/
│       ├── Services/
│       │   ├── Interfaces/
│       │   │   ├── IWatchlistService.cs
│       │   │   ├── INewsService.cs
│       │   │   ├── ISourceCrawler.cs
│       │   │   ├── INotificationService.cs
│       │   │   └── IScheduler.cs
│       │   ├── WatchlistService.cs
│       │   ├── NewsService.cs
│       │   ├── NotificationService.cs
│       │   ├── ChannelScheduler.cs
│       │   └── Crawlers/
│       │       ├── YahooFinanceCrawler.cs
│       │       ├── ReutersCrawler.cs
│       │       ├── GoogleFinanceCrawler.cs
│       │       ├── InvestingCrawler.cs
│       │       └── WSJCrawler.cs
│       ├── BackgroundServices/
│       │   └── NewsPollerHostedService.cs
│       ├── Utilities/
│       │   ├── DedupeHelper.cs
│       │   ├── TimeParser.cs
│       │   ├── UrlCanonicalizer.cs
│       │   └── PollyPolicies.cs
│       ├── ViewModels/
│       │   ├── MainViewModel.cs
│       │   ├── WatchItemViewModel.cs
│       │   └── NewsItemViewModel.cs
│       └── Views/
│           ├── MainWindow.xaml
│           ├── MainWindow.xaml.cs
│           ├── NewsViewWindow.xaml
│           ├── NewsViewWindow.xaml.cs
│           ├── AddWatchDialog.xaml
│           ├── AddWatchDialog.xaml.cs
│           ├── EditSourcePoolDialog.xaml
│           └── EditSourcePoolDialog.xaml.cs
└── tests/
    └── StockNewsNotifier.Tests/
        ├── StockNewsNotifier.Tests.csproj
        ├── Crawlers/
        │   └── YahooFinanceCrawlerTests.cs
        └── Fixtures/
            └── (HTML test files)
```

## Implementation Phases

### Phase 1: Foundation (Priority 1)
**Goal**: Set up project structure, database, and basic services

#### Step 1.1: Project Setup
```bash
# Create solution and project
dotnet new sln -n StockNewsNotifier
dotnet new wpf -n StockNewsNotifier -o src/StockNewsNotifier
dotnet sln add src/StockNewsNotifier/StockNewsNotifier.csproj

# Add NuGet packages
cd src/StockNewsNotifier
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package AngleSharp
dotnet add package Serilog
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Extensions.Hosting
dotnet add package Polly
dotnet add package Microsoft.Toolkit.Uwp.Notifications
dotnet add package System.Threading.Channels
```

#### Step 1.2: Data Models
Create all entity classes in `Data/Entities/`:
- `WatchItem.cs` - Watched stock ticker
- `Source.cs` - News source (Yahoo, Reuters, etc.)
- `WatchItemSource.cs` - Many-to-many relationship
- `NewsItem.cs` - Crawled news article
- `CrawlState.cs` - Rate limiting and robots.txt cache

**Key fields**:
- WatchItem: Id (Guid), Exchange, Ticker, CompanyName, IconUrl, AlertsEnabled, CreatedUtc
- NewsItem: CanonicalUrl, TitleHash, SimHash64, IsRead, PublishedUtc, FetchedUtc

#### Step 1.3: AppDbContext
Create `Data/AppDbContext.cs`:
- Configure EF Core entities
- Add fluent API configurations for indexes:
  - `IX_WatchItem_Exchange_Ticker` (unique)
  - `IX_NewsItem_CanonicalUrl` (unique)
  - `IX_NewsItem_WatchItemId_FetchedUtc`

#### Step 1.4: Generic Host Bootstrap
Modify `App.xaml.cs`:
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

public partial class App : Application
{
    private IHost _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        _host = Host.CreateDefaultBuilder()
            .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration))
            .ConfigureServices((context, services) =>
            {
                // Database
                services.AddDbContext<AppDbContext>(options => 
                    options.UseSqlite($"Data Source={GetDbPath()}"));
                
                // Services
                services.AddSingleton<INotificationService, WindowsToastNotificationService>();
                services.AddSingleton<IScheduler, ChannelScheduler>();
                services.AddScoped<IWatchlistService, WatchlistService>();
                services.AddScoped<INewsService, NewsService>();
                
                // HttpClient
                services.AddHttpClient("crawler")
                    .AddPolicyHandler(PollyPolicies.GetRetryPolicy());
                
                // Crawlers (start with Yahoo only)
                services.AddSingleton<ISourceCrawler, YahooFinanceCrawler>();
                
                // Background service
                services.AddHostedService<NewsPollerHostedService>();
                
                // ViewModels
                services.AddSingleton<MainViewModel>();
            })
            .Build();

        await _host.StartAsync();
        
        // Show tray window
        var mainVM = _host.Services.GetRequiredService<MainViewModel>();
        // Initialize NotifyIcon here
    }

    private string GetDbPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "StockNewsNotifier");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "news.db");
    }
}
```

#### Step 1.5: appsettings.json
```json
{
  "Polling": {
    "DefaultIntervalSeconds": 240,
    "JitterSeconds": 30
  },
  "RateLimits": {
    "DefaultRps": 1,
    "DefaultPerMinute": 10,
    "PerHostOverrides": {
      "finance.yahoo.com": { "Rps": 1, "PerMinute": 12 }
    }
  },
  "Notifications": {
    "MaxPerTickerPer10Min": 5,
    "FreshnessThresholdHours": 24
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "Logs/app-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### Phase 2: Core Services (Priority 1)

#### Step 2.1: Interfaces
Create all interfaces in `Services/Interfaces/`:

```csharp
// Ticker.cs
public record Ticker(string Exchange, string Symbol)
{
    public override string ToString() => $"{Exchange}:{Symbol}";
}

// IWatchlistService.cs
public interface IWatchlistService
{
    Task<WatchItem> AddAsync(Ticker ticker, CancellationToken ct = default);
    Task RemoveAsync(Guid watchItemId, CancellationToken ct = default);
    Task SetAlertsAsync(Guid watchItemId, bool enabled, CancellationToken ct = default);
    Task<IReadOnlyList<WatchItem>> ListAsync(CancellationToken ct = default);
}

// ISourceCrawler.cs
public interface ISourceCrawler
{
    string Name { get; }
    string BaseHost { get; }
    IReadOnlyList<string> BuildQueryUrls(WatchItem watch);
    Task<IReadOnlyList<RawArticle>> FetchAsync(string url, CancellationToken ct);
}

public record RawArticle(
    string Title,
    string Url,
    DateTime? PublishedUtc,
    string? Summary
);

// INewsService.cs
public interface INewsService
{
    Task<int> IngestAsync(WatchItem watch, int sourceId, IEnumerable<RawArticle> items, CancellationToken ct);
    Task<IReadOnlyList<NewsItem>> ListAsync(Guid watchItemId, int days, bool unreadOnly, CancellationToken ct);
    Task MarkReadAsync(Guid newsId, bool isRead, CancellationToken ct);
}

// INotificationService.cs
public interface INotificationService
{
    Task NotifyAsync(NewsItem item, CancellationToken ct);
}

// IScheduler.cs
public interface IScheduler
{
    void EnqueueCrawl(Guid watchItemId);
}
```

#### Step 2.2: Utility Classes

**DedupeHelper.cs**:
```csharp
using System.Security.Cryptography;
using System.Text;

public static class DedupeHelper
{
    public static string ComputeTitleHash(string title)
    {
        var normalized = NormalizeTitle(title);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }
    
    public static long ComputeSimHash(string text)
    {
        // Simplified SimHash implementation
        // For MVP, can use TitleHash only
        return 0; // TODO: Implement proper SimHash
    }
    
    private static string NormalizeTitle(string title)
    {
        return title.Trim().ToLowerInvariant();
    }
}
```

**UrlCanonicalizer.cs**:
```csharp
public static class UrlCanonicalizer
{
    public static string Canonicalize(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;
        
        // Strip tracking parameters
        var builder = new UriBuilder(uri);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        
        // Remove common tracking params
        var trackingParams = new[] { "utm_source", "utm_medium", "utm_campaign", "ref", "src" };
        foreach (var param in trackingParams)
            query.Remove(param);
        
        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }
}
```

**TimeParser.cs**:
```csharp
using System.Globalization;
using System.Text.RegularExpressions;

public static class TimeParser
{
    public static DateTime? ParseRelativeTime(string relativeTime, DateTime anchor)
    {
        // Parse "2 hours ago", "3 days ago", etc.
        var match = Regex.Match(relativeTime, @"(\d+)\s*(minute|hour|day)s?\s*ago", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;
        
        var value = int.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value.ToLower();
        
        return unit switch
        {
            "minute" => anchor.AddMinutes(-value),
            "hour" => anchor.AddHours(-value),
            "day" => anchor.AddDays(-value),
            _ => null
        };
    }
    
    public static DateTime? ParseAbsoluteTime(string dateString)
    {
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
            return result.ToUniversalTime();
        return null;
    }
}
```

**PollyPolicies.cs**:
```csharp
using Polly;
using Polly.Extensions.Http;

public static class PollyPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry
                });
    }
}
```

### Phase 3: Yahoo Finance Crawler (Priority 1 - MVP)

Create `Services/Crawlers/YahooFinanceCrawler.cs`:

```csharp
using AngleSharp;
using AngleSharp.Html.Dom;

public class YahooFinanceCrawler : ISourceCrawler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YahooFinanceCrawler> _logger;

    public string Name => "YahooFinance";
    public string BaseHost => "finance.yahoo.com";

    public IReadOnlyList<string> BuildQueryUrls(WatchItem watch)
    {
        // Yahoo Finance news URL format
        return new[] { $"https://finance.yahoo.com/quote/{watch.Ticker}/news" };
    }

    public async Task<IReadOnlyList<RawArticle>> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("crawler");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("StockNewsNotifier/1.0");
            
            var html = await client.GetStringAsync(url, ct);
            return ParseNewsPage(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch from {Url}", url);
            return Array.Empty<RawArticle>();
        }
    }

    private List<RawArticle> ParseNewsPage(string html)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = context.OpenAsync(req => req.Content(html)).Result;
        
        var articles = new List<RawArticle>();
        
        // TODO: Adjust selectors based on actual Yahoo Finance HTML structure
        var newsItems = document.QuerySelectorAll("li.js-stream-content");
        
        foreach (var item in newsItems)
        {
            try
            {
                var titleElement = item.QuerySelector("h3 a");
                if (titleElement == null) continue;
                
                var title = titleElement.TextContent.Trim();
                var url = titleElement.GetAttribute("href");
                
                // Make URL absolute
                if (url?.StartsWith("/") == true)
                    url = $"https://finance.yahoo.com{url}";
                
                // Parse time
                var timeElement = item.QuerySelector("time");
                var publishedUtc = timeElement != null 
                    ? TimeParser.ParseRelativeTime(timeElement.TextContent, DateTime.UtcNow)
                    : null;
                
                articles.Add(new RawArticle(title, url, publishedUtc, null));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse news item");
            }
        }
        
        return articles;
    }
}
```

**IMPORTANT NOTE**: The HTML selectors above are PLACEHOLDERS. You MUST:
1. Visit https://finance.yahoo.com/quote/MSFT/news in a browser
2. Inspect the actual HTML structure
3. Update the CSS selectors accordingly
4. Test with real HTML and save fixtures for unit tests

### Phase 4: Service Implementations (Priority 1)

#### WatchlistService.cs
```csharp
public class WatchlistService : IWatchlistService
{
    private readonly AppDbContext _db;
    private readonly ILogger<WatchlistService> _logger;

    public async Task<WatchItem> AddAsync(Ticker ticker, CancellationToken ct = default)
    {
        // Check if exists
        var existing = await _db.WatchItems
            .FirstOrDefaultAsync(w => w.Exchange == ticker.Exchange && w.Ticker == ticker.Symbol, ct);
        
        if (existing != null)
            return existing;
        
        var watchItem = new WatchItem
        {
            Id = Guid.NewGuid(),
            Exchange = ticker.Exchange,
            Ticker = ticker.Symbol,
            AlertsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        };
        
        _db.WatchItems.Add(watchItem);
        
        // Add default source (Yahoo Finance)
        var yahooSource = await _db.Sources.FirstOrDefaultAsync(s => s.Name == "YahooFinance", ct);
        if (yahooSource != null)
        {
            _db.WatchItemSources.Add(new WatchItemSource
            {
                WatchItemId = watchItem.Id,
                SourceId = yahooSource.Id
            });
        }
        
        await _db.SaveChangesAsync(ct);
        return watchItem;
    }

    public async Task RemoveAsync(Guid watchItemId, CancellationToken ct = default)
    {
        var item = await _db.WatchItems.FindAsync(new object[] { watchItemId }, ct);
        if (item != null)
        {
            _db.WatchItems.Remove(item);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task SetAlertsAsync(Guid watchItemId, bool enabled, CancellationToken ct = default)
    {
        var item = await _db.WatchItems.FindAsync(new object[] { watchItemId }, ct);
        if (item != null)
        {
            item.AlertsEnabled = enabled;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<WatchItem>> ListAsync(CancellationToken ct = default)
    {
        return await _db.WatchItems
            .Include(w => w.WatchItemSources)
            .ThenInclude(ws => ws.Source)
            .ToListAsync(ct);
    }
}
```

#### NewsService.cs
```csharp
public class NewsService : INewsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NewsService> _logger;

    public async Task<int> IngestAsync(WatchItem watch, int sourceId, IEnumerable<RawArticle> items, CancellationToken ct)
    {
        var newCount = 0;
        
        foreach (var article in items)
        {
            var canonicalUrl = UrlCanonicalizer.Canonicalize(article.Url);
            
            // Check if exists
            var exists = await _db.NewsItems.AnyAsync(
                n => n.CanonicalUrl == canonicalUrl, ct);
            
            if (exists)
                continue;
            
            var newsItem = new NewsItem
            {
                Id = Guid.NewGuid(),
                WatchItemId = watch.Id,
                SourceId = sourceId,
                Title = article.Title,
                Url = article.Url,
                CanonicalUrl = canonicalUrl,
                Summary = article.Summary,
                PublishedUtc = article.PublishedUtc,
                FetchedUtc = DateTime.UtcNow,
                TitleHash = DedupeHelper.ComputeTitleHash(article.Title),
                SimHash64 = DedupeHelper.ComputeSimHash(article.Title),
                IsRead = false
            };
            
            _db.NewsItems.Add(newsItem);
            newCount++;
        }
        
        if (newCount > 0)
            await _db.SaveChangesAsync(ct);
        
        return newCount;
    }

    public async Task<IReadOnlyList<NewsItem>> ListAsync(Guid watchItemId, int days, bool unreadOnly, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        
        var query = _db.NewsItems
            .Where(n => n.WatchItemId == watchItemId && n.FetchedUtc >= cutoff);
        
        if (unreadOnly)
            query = query.Where(n => !n.IsRead);
        
        return await query
            .OrderByDescending(n => n.PublishedUtc ?? n.FetchedUtc)
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(Guid newsId, bool isRead, CancellationToken ct)
    {
        var item = await _db.NewsItems.FindAsync(new object[] { newsId }, ct);
        if (item != null)
        {
            item.IsRead = isRead;
            await _db.SaveChangesAsync(ct);
        }
    }
}
```

### Phase 5: Background Polling (Priority 1)

#### ChannelScheduler.cs
```csharp
using System.Threading.Channels;

public class ChannelScheduler : IScheduler
{
    private readonly Channel<Guid> _channel;
    
    public ChannelScheduler()
    {
        _channel = Channel.CreateUnbounded<Guid>();
    }
    
    public void EnqueueCrawl(Guid watchItemId)
    {
        _channel.Writer.TryWrite(watchItemId);
    }
    
    public ChannelReader<Guid> Reader => _channel.Reader;
}
```

#### NewsPollerHostedService.cs
```csharp
public class NewsPollerHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ChannelScheduler _scheduler;
    private readonly IConfiguration _config;
    private readonly ILogger<NewsPollerHostedService> _logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Start polling loop
        _ = Task.Run(() => PollWatchlistAsync(ct), ct);
        
        // Process crawl jobs
        await foreach (var watchItemId in _scheduler.Reader.ReadAllAsync(ct))
        {
            await ProcessCrawlJobAsync(watchItemId, ct);
        }
    }

    private async Task PollWatchlistAsync(CancellationToken ct)
    {
        var intervalSeconds = _config.GetValue<int>("Polling:DefaultIntervalSeconds", 240);
        var jitterSeconds = _config.GetValue<int>("Polling:JitterSeconds", 30);
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var watchlist = scope.ServiceProvider.GetRequiredService<IWatchlistService>();
                
                var watches = await watchlist.ListAsync(ct);
                foreach (var watch in watches)
                {
                    _scheduler.EnqueueCrawl(watch.Id);
                }
                
                var delay = intervalSeconds + Random.Shared.Next(-jitterSeconds, jitterSeconds);
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling loop");
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
        }
    }

    private async Task ProcessCrawlJobAsync(Guid watchItemId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var crawlers = scope.ServiceProvider.GetServices<ISourceCrawler>();
        
        var watchItem = await db.WatchItems
            .Include(w => w.WatchItemSources)
            .ThenInclude(ws => ws.Source)
            .FirstOrDefaultAsync(w => w.Id == watchItemId, ct);
        
        if (watchItem == null)
            return;
        
        foreach (var watchSource in watchItem.WatchItemSources)
        {
            var crawler = crawlers.FirstOrDefault(c => c.Name == watchSource.Source.Name);
            if (crawler == null || !watchSource.Source.Enabled)
                continue;
            
            try
            {
                var urls = crawler.BuildQueryUrls(watchItem);
                foreach (var url in urls)
                {
                    var articles = await crawler.FetchAsync(url, ct);
                    var newCount = await newsService.IngestAsync(watchItem, watchSource.SourceId, articles, ct);
                    
                    _logger.LogInformation("Ingested {Count} new articles for {Ticker} from {Source}", 
                        newCount, watchItem.Ticker, crawler.Name);
                    
                    // Send notifications for new items
                    if (newCount > 0 && watchItem.AlertsEnabled)
                    {
                        var recentNews = await newsService.ListAsync(watchItemId, 1, true, ct);
                        foreach (var news in recentNews.Take(newCount))
                        {
                            await notificationService.NotifyAsync(news, ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling {Source} for {Ticker}", 
                    crawler.Name, watchItem.Ticker);
            }
        }
    }
}
```

### Phase 6: UI Implementation (Priority 2)

#### MainWindow.xaml
```xml
<Window x:Class="StockNewsNotifier.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Stock News Notifier" 
        Width="400" Height="500"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="White"
        ShowInTaskbar="False">
    <Border BorderBrush="Gray" BorderThickness="1">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="40"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>
            
            <!-- Header -->
            <Grid Grid.Row="0" Background="#F5F5F5">
                <Button Content="+" Width="30" Height="30" 
                        HorizontalAlignment="Left" Margin="5"
                        Command="{Binding AddWatchCommand}"/>
                <TextBlock Text="Stock News Notifier" 
                           VerticalAlignment="Center" 
                           HorizontalAlignment="Center"
                           FontWeight="Bold"/>
            </Grid>
            
            <!-- Watch list -->
            <ItemsControl Grid.Row="1" ItemsSource="{Binding WatchItems}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border BorderBrush="LightGray" BorderThickness="0,0,0,1" 
                                Padding="10" Background="White">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="40"/>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="30"/>
                                </Grid.ColumnDefinitions>
                                
                                <!-- Icon -->
                                <Image Grid.Column="0" 
                                       Source="{Binding IconUrl}" 
                                       Width="32" Height="32"/>
                                
                                <!-- Ticker -->
                                <StackPanel Grid.Column="1" VerticalAlignment="Center">
                                    <TextBlock Text="{Binding DisplayName}" FontWeight="Bold"/>
                                    <TextBlock Text="{Binding CompanyName}" 
                                               FontSize="10" Foreground="Gray"/>
                                </StackPanel>
                                
                                <!-- Alert bell -->
                                <Button Grid.Column="2" Content="🔔" 
                                        Command="{Binding ToggleAlertsCommand}"/>
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </Grid>
    </Border>
</Window>
```

#### NotifyIcon Integration
In `MainWindow.xaml.cs`:
```csharp
using System.Windows.Forms; // Add reference to System.Windows.Forms

public partial class MainWindow : Window
{
    private NotifyIcon _notifyIcon;
    
    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
    }
    
    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = new System.Drawing.Icon("app.ico"), // Create an icon file
            Visible = true,
            Text = "Stock News Notifier"
        };
        
        _notifyIcon.Click += (s, e) =>
        {
            Show();
            Activate();
        };
        
        // Hide window instead of closing
        Closing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }
}
```

### Phase 7: Windows Toast Notifications (Priority 2)

#### WindowsToastNotificationService.cs
```csharp
using Microsoft.Toolkit.Uwp.Notifications;

public class WindowsToastNotificationService : INotificationService
{
    private readonly ILogger<WindowsToastNotificationService> _logger;
    
    public async Task NotifyAsync(NewsItem item, CancellationToken ct)
    {
        try
        {
            new ToastContentBuilder()
                .AddText($"New article: {item.WatchItem.Ticker}")
                .AddText(item.Title)
                .AddButton(new ToastButton()
                    .SetContent("Open")
                    .AddArgument("action", "open")
                    .AddArgument("newsId", item.Id.ToString()))
                .AddButton(new ToastButton()
                    .SetContent("Mark Read")
                    .AddArgument("action", "markRead")
                    .AddArgument("newsId", item.Id.ToString()))
                .Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification");
        }
    }
}
```

## Database Migration

After setting up entities, run:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Testing Strategy

1. **Unit Tests**: Test each crawler with saved HTML fixtures
2. **Integration Tests**: Test service layer with in-memory SQLite
3. **Manual Testing**: 
   - Add NASDAQ:MSFT
   - Verify news appears
   - Check notifications
   - Test read/unread states

## Common Issues & Solutions

### Issue: HTML Parsing Fails
- **Solution**: Update CSS selectors by inspecting actual website HTML
- Save HTML fixtures for regression testing

### Issue: Rate Limiting
- **Solution**: Increase delay in `PollyPolicies`, check domain-specific limits

### Issue: Toast Notifications Don't Appear
- **Solution**: Ensure app has an AUMID registered in Windows

### Issue: Database Locked
- **Solution**: Use `EnablePooling=false` in connection string for SQLite

## Development Workflow

1. Implement Phase 1 (Foundation)
2. Run migrations
3. Implement Phase 2 (Services)
4. Implement Phase 3 (Yahoo Crawler)
5. Test end-to-end with one ticker
6. Implement Phase 4-5 (Background polling)
7. Implement Phase 6 (UI)
8. Implement Phase 7 (Notifications)
9. Add remaining crawlers (Reuters, Google Finance, etc.)

## Next Steps After MVP

1. Add remaining crawlers (Reuters, Google Finance, Investing, WSJ)
2. Implement source pool editing UI
3. Add news view window with read/unread grouping
4. Implement snooze functionality
5. Add custom query per source
6. Improve dedupe with proper SimHash
7. Add unit tests with HTML fixtures

## Critical Notes

- **ALWAYS** inspect actual website HTML before finalizing crawler selectors
- **ALWAYS** respect robots.txt and rate limits
- **ALWAYS** handle exceptions gracefully in crawlers
- **TEST** with real data before considering any phase complete
- The design document is comprehensive - follow it closely
- Focus on MVP first (Yahoo Finance only), then expand

## Resources

- [AngleSharp Documentation](https://anglesharp.github.io/)
- [EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [Windows Toast Notifications](https://docs.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/adaptive-interactive-toasts)
- [Polly Resilience](https://github.com/App-vNext/Polly)
