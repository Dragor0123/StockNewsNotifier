using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly Dictionary<string, RateLimitSettings> _rateLimitCache = new(StringComparer.OrdinalIgnoreCase);

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
            finally
            {
                _scheduler.MarkCompleted(watchItemId);
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
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

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

            await CrawlSourceAsync(watchItem, watchSource.Source, crawler, newsService, notificationService, db, httpClientFactory, ct);
        }
    }

    private async Task CrawlSourceAsync(
        WatchItem watchItem,
        Source source,
        ISourceCrawler crawler,
        INewsService newsService,
        INotificationService notificationService,
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        CrawlState? crawlState = null;

        try
        {
            var totalNewCount = 0;
            var urls = crawler.BuildQueryUrls(watchItem);
            crawlState = await GetOrCreateCrawlStateAsync(source, crawler, db, ct);
            await EnsureRobotsTxtAsync(crawlState, source, crawler, db, httpClientFactory, ct);

            foreach (var url in urls)
            {
                await RespectRateLimitAsync(crawlState, source, ct);

                var articles = await crawler.FetchAsync(url, ct);
                var newCount = await newsService.IngestAsync(watchItem, source.Id, articles, ct);
                totalNewCount += newCount;

                _logger.LogInformation("Crawl {Source} for {Ticker}: {NewCount} new articles",
                    source.Name, watchItem.Ticker, newCount);

                await RecordCrawlSuccessAsync(crawlState, db, ct);
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
            if (crawlState != null)
            {
                await RecordCrawlFailureAsync(crawlState, ex, db, ct);
            }

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

    private async Task<CrawlState> GetOrCreateCrawlStateAsync(
        Source source,
        ISourceCrawler crawler,
        AppDbContext db,
        CancellationToken ct)
    {
        var state = await db.CrawlStates.FirstOrDefaultAsync(cs => cs.SourceId == source.Id, ct);
        var settings = GetRateLimitSettings(source, crawler);
        var needsSave = false;

        if (state == null)
        {
            state = new CrawlState
            {
                SourceId = source.Id,
                RequestsPerSecond = settings.RequestsPerSecond,
                RequestsPerMinute = settings.RequestsPerMinute
            };

            db.CrawlStates.Add(state);
            needsSave = true;
        }
        else
        {
            if (Math.Abs(state.RequestsPerSecond - settings.RequestsPerSecond) > double.Epsilon)
            {
                state.RequestsPerSecond = settings.RequestsPerSecond;
                needsSave = true;
            }

            if (state.RequestsPerMinute != settings.RequestsPerMinute)
            {
                state.RequestsPerMinute = settings.RequestsPerMinute;
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await db.SaveChangesAsync(ct);
        }

        return state;
    }

    private RateLimitSettings GetRateLimitSettings(Source source, ISourceCrawler crawler)
    {
        var defaultRps = Math.Max(0.1, _configuration.GetValue<double>("RateLimits:DefaultRps", 1));
        var defaultPerMinute = Math.Max(1, _configuration.GetValue<int>("RateLimits:DefaultPerMinute", 10));
        var host = GetHostForSource(source, crawler);

        if (host == null)
        {
            return new RateLimitSettings(defaultRps, defaultPerMinute);
        }

        if (_rateLimitCache.TryGetValue(host, out var cached))
        {
            return cached;
        }

        var overridesSection = _configuration.GetSection($"RateLimits:PerHostOverrides:{host}");
        var rps = overridesSection.Exists() ? overridesSection.GetValue<double?>("Rps") ?? defaultRps : defaultRps;
        var perMinute = overridesSection.Exists() ? overridesSection.GetValue<int?>("PerMinute") ?? defaultPerMinute : defaultPerMinute;

        var settings = new RateLimitSettings(
            RequestsPerSecond: rps <= 0 ? defaultRps : rps,
            RequestsPerMinute: perMinute <= 0 ? defaultPerMinute : perMinute);

        _rateLimitCache[host] = settings;
        return settings;
    }

    private static string? GetHostForSource(Source source, ISourceCrawler crawler)
    {
        if (!string.IsNullOrWhiteSpace(source.BaseUrl) &&
            Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return string.IsNullOrWhiteSpace(crawler.BaseHost) ? null : crawler.BaseHost;
    }

    private async Task RespectRateLimitAsync(CrawlState state, Source source, CancellationToken ct)
    {
        var delay = CalculateRateLimitDelay(state);
        if (delay > TimeSpan.Zero)
        {
            _logger.LogDebug("Delaying {DelaySeconds:F1}s for {Source} to respect rate limits",
                delay.TotalSeconds, source.Name);
            await Task.Delay(delay, ct);
        }
    }

    private static TimeSpan CalculateRateLimitDelay(CrawlState state)
    {
        if (!state.LastCrawlUtc.HasValue)
        {
            return TimeSpan.Zero;
        }

        var intervalSeconds = 0d;
        if (state.RequestsPerSecond > 0)
        {
            intervalSeconds = Math.Max(intervalSeconds, 1d / state.RequestsPerSecond);
        }

        if (state.RequestsPerMinute > 0)
        {
            intervalSeconds = Math.Max(intervalSeconds, 60d / state.RequestsPerMinute);
        }

        if (intervalSeconds <= 0)
        {
            return TimeSpan.Zero;
        }

        var nextTime = state.LastCrawlUtc.Value.AddSeconds(intervalSeconds);
        var wait = nextTime - DateTime.UtcNow;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    private static Task RecordCrawlSuccessAsync(CrawlState state, AppDbContext db, CancellationToken ct)
    {
        state.LastCrawlUtc = DateTime.UtcNow;
        state.ConsecutiveErrors = 0;
        state.LastError = null;
        state.LastErrorUtc = null;
        return db.SaveChangesAsync(ct);
    }

    private static Task RecordCrawlFailureAsync(CrawlState state, Exception ex, AppDbContext db, CancellationToken ct)
    {
        state.ConsecutiveErrors += 1;
        state.LastError = ex.Message;
        state.LastErrorUtc = DateTime.UtcNow;
        return db.SaveChangesAsync(ct);
    }

    private async Task EnsureRobotsTxtAsync(
        CrawlState state,
        Source source,
        ISourceCrawler crawler,
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var cacheDuration = GetRobotsCacheDuration();
        if (state.RobotsTxtFetchedUtc.HasValue &&
            state.RobotsTxtFetchedUtc.Value >= DateTime.UtcNow - cacheDuration)
        {
            return;
        }

        var host = GetHostForSource(source, crawler);
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("Unable to determine host for {Source} when fetching robots.txt", source.Name);
            return;
        }

        var robotsUrl = $"https://{host.TrimEnd('/')}/robots.txt";

        try
        {
            var client = httpClientFactory.CreateClient("crawler");
            var robotsText = await client.GetStringAsync(robotsUrl, ct);

            if (robotsText.Length > 10000)
            {
                robotsText = robotsText[..10000];
            }

            state.RobotsTxt = robotsText;
            state.RobotsTxtFetchedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Updated robots.txt cache for {Source}", source.Name);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch robots.txt for {Source}", source.Name);
        }
    }

    private TimeSpan GetRobotsCacheDuration()
    {
        var hours = Math.Max(1, _configuration.GetValue<int>("Crawler:RobotsCacheHours", 24));
        return TimeSpan.FromHours(hours);
    }

    private sealed record RateLimitSettings(double RequestsPerSecond, int RequestsPerMinute);
}
