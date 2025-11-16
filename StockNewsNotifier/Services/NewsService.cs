using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockNewsNotifier.Data;
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services.Interfaces;
using StockNewsNotifier.Utilities;

namespace StockNewsNotifier.Services;

/// <summary>
/// Service for managing news items
/// </summary>
public class NewsService : INewsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NewsService> _logger;

    public NewsService(AppDbContext db, ILogger<NewsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> IngestAsync(WatchItem watch, int sourceId, IEnumerable<RawArticle> items, CancellationToken ct)
    {
        var newCount = 0;
        var fetchedUtc = DateTime.UtcNow;

        foreach (var article in items)
        {
            try
            {
                // Canonicalize URL to remove tracking parameters
                var canonicalUrl = UrlCanonicalizer.Canonicalize(article.Url);

                // Check if this article already exists (by canonical URL)
                var exists = await _db.NewsItems
                    .AnyAsync(n => n.CanonicalUrl == canonicalUrl, ct);

                if (exists)
                {
                    _logger.LogDebug("Skipping duplicate article: {Title}", article.Title);
                    continue;
                }

                // Compute title hash for deduplication
                var titleHash = DedupeHelper.ComputeTitleHash(article.Title);

                // Check if article with same title hash exists
                // (This catches exact duplicates with different URLs)
                var titleDuplicate = await _db.NewsItems
                    .AnyAsync(n => n.TitleHash == titleHash && n.WatchItemId == watch.Id, ct);

                if (titleDuplicate)
                {
                    _logger.LogDebug("Skipping title duplicate: {Title}", article.Title);
                    continue;
                }

                // Create new news item
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
                    FetchedUtc = fetchedUtc,
                    TitleHash = titleHash,
                    SimHash64 = DedupeHelper.ComputeSimHash(article.Title), // MVP: Always 0
                    IsRead = false,
                    NotificationSent = false
                };

                _db.NewsItems.Add(newsItem);
                newCount++;

                _logger.LogDebug("Added new article: {Title}", article.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting article: {Title}", article.Title);
                // Continue processing other articles
            }
        }

        if (newCount > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Ingested {NewCount} new articles for {Exchange}:{Ticker} from source {SourceId}",
                newCount, watch.Exchange, watch.Ticker, sourceId);
        }

        return newCount;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NewsItem>> ListAsync(Guid watchItemId, int days, bool unreadOnly, CancellationToken ct)
    {
        var cutoffUtc = DateTime.UtcNow.AddDays(-days);

        var query = _db.NewsItems
            .Where(n => n.WatchItemId == watchItemId && n.FetchedUtc >= cutoffUtc);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var items = await query
            .Include(n => n.Source)
            .Include(n => n.WatchItem)
            .OrderByDescending(n => n.PublishedUtc ?? n.FetchedUtc)
            .ToListAsync(ct);

        _logger.LogDebug("Retrieved {Count} news items for watch item {WatchItemId} (days: {Days}, unreadOnly: {UnreadOnly})",
            items.Count, watchItemId, days, unreadOnly);

        return items;
    }

    /// <inheritdoc/>
    public async Task MarkReadAsync(Guid newsId, bool isRead, CancellationToken ct)
    {
        var item = await _db.NewsItems.FindAsync(new object[] { newsId }, ct);

        if (item == null)
        {
            _logger.LogWarning("News item {NewsId} not found for mark read update", newsId);
            return;
        }

        item.IsRead = isRead;
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Marked news item {NewsId} as {Status}", newsId, isRead ? "read" : "unread");
    }
}
