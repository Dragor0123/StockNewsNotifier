using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;
using StockNewsNotifier.Data;
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services;
using StockNewsNotifier.Services.Interfaces;

namespace StockNewsNotifier.BackgroundServices;

/// <summary>
/// Background service that periodically polls watch items and triggers crawlers.
/// </summary>
public class NewsPollerHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ChannelScheduler _scheduler;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NewsPollerHostedService> _logger;

    public NewsPollerHostedService(
        IServiceProvider services,
        ChannelScheduler scheduler,
        IConfiguration configuration,
        ILogger<NewsPollerHostedService> logger)
    {
        _services = services;
        _scheduler = scheduler;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting news poller hosted service");

        _ = Task.Run(() => PollWatchlistAsync(stoppingToken), stoppingToken);

        await foreach (var watchItemId in _scheduler.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessWatchItemAsync(watchItemId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing crawl job for {WatchItemId}", watchItemId);
            }
        }

        _logger.LogInformation("News poller hosted service stopped");
    }

    private async Task PollWatchlistAsync(CancellationToken ct)
    {
        var baseInterval = Math.Max(30, _configuration.GetValue<int>("Polling:DefaultIntervalSeconds", 240));
        var jitter = Math.Max(0, _configuration.GetValue<int>("Polling:JitterSeconds", 30));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var watchlistService = scope.ServiceProvider.GetRequiredService<IWatchlistService>();
                var watches = await watchlistService.ListAsync(ct);

                foreach (var watch in watches)
                {
                    _scheduler.EnqueueCrawl(watch.Id);
                }

                var jitterOffset = jitter == 0 ? 0 : Random.Shared.Next(-jitter, jitter + 1);
                var delaySeconds = Math.Max(10, baseInterval + jitterOffset);
                _logger.LogDebug("Poll loop sleeping for {DelaySeconds}s", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while enqueuing watch items");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }

    private async Task ProcessWatchItemAsync(Guid watchItemId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var crawlers = scope.ServiceProvider.GetServices<ISourceCrawler>().ToList();

        var watchItem = await db.WatchItems
            .Include(w => w.WatchItemSources)
            .ThenInclude(ws => ws.Source)
            .FirstOrDefaultAsync(w => w.Id == watchItemId, ct);

        if (watchItem == null)
        {
            _logger.LogWarning("Watch item {WatchItemId} not found when processing crawl job", watchItemId);
            return;
        }

        foreach (var watchSource in watchItem.WatchItemSources.Where(ws => ws.Enabled && ws.Source != null && ws.Source.Enabled))
        {
            if (watchSource.Source == null)
            {
                continue;
            }

            var crawler = crawlers.FirstOrDefault(c =>
                string.Equals(c.Name, watchSource.Source.Name, StringComparison.OrdinalIgnoreCase));

            if (crawler == null)
            {
                _logger.LogWarning("Crawler {SourceName} not registered; skipping watch item {WatchItemId}",
                    watchSource.Source.Name, watchItemId);
                continue;
            }

            await CrawlSourceAsync(watchItem, watchSource.Source, crawler, newsService, notificationService, db, ct);
        }
    }

    private async Task CrawlSourceAsync(
        WatchItem watchItem,
        Source source,
        ISourceCrawler crawler,
        INewsService newsService,
        INotificationService notificationService,
        AppDbContext db,
        CancellationToken ct)
    {
        try
        {
            var totalNewCount = 0;
            var urls = crawler.BuildQueryUrls(watchItem);

            foreach (var url in urls)
            {
                var articles = await crawler.FetchAsync(url, ct);
                var newCount = await newsService.IngestAsync(watchItem, source.Id, articles, ct);
                totalNewCount += newCount;

                _logger.LogInformation("Crawl {Source} for {Ticker}: {NewCount} new articles",
                    source.Name, watchItem.Ticker, newCount);
            }

            if (totalNewCount > 0 && watchItem.AlertsEnabled)
            {
                await SendNotificationsAsync(watchItem.Id, totalNewCount, notificationService, db, _logger, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling {Source} for {Ticker}", source.Name, watchItem.Ticker);
        }
    }

    private static async Task SendNotificationsAsync(
        Guid watchItemId,
        int limit,
        INotificationService notificationService,
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var unsentItems = await db.NewsItems
            .Include(n => n.WatchItem)
            .Include(n => n.Source)
            .Where(n => n.WatchItemId == watchItemId && !n.NotificationSent)
            .OrderByDescending(n => n.PublishedUtc ?? n.FetchedUtc)
            .Take(limit)
            .ToListAsync(ct);

        foreach (var news in unsentItems)
        {
            try
            {
                await notificationService.NotifyAsync(news, ct);
                news.NotificationSent = true;
            }
            catch (Exception ex)
            {
                // Keep the flag false so we can retry later
                logger.LogError(ex, "Failed to send notification for news {NewsId}", news.Id);
            }
        }

        if (unsentItems.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
