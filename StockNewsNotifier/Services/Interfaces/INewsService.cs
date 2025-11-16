using StockNewsNotifier.Data.Entities;

namespace StockNewsNotifier.Services.Interfaces;

/// <summary>
/// Service for managing news items
/// </summary>
public interface INewsService
{
    /// <summary>
    /// Ingest raw articles into the database
    /// </summary>
    /// <param name="watch">Watch item these articles belong to</param>
    /// <param name="sourceId">ID of the source these articles came from</param>
    /// <param name="items">Raw articles to ingest</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of new articles added (duplicates are skipped)</returns>
    Task<int> IngestAsync(WatchItem watch, int sourceId, IEnumerable<RawArticle> items, CancellationToken ct);

    /// <summary>
    /// List news items for a specific watch item
    /// </summary>
    /// <param name="watchItemId">ID of the watch item</param>
    /// <param name="days">How many days back to fetch</param>
    /// <param name="unreadOnly">Whether to return only unread items</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of news items</returns>
    Task<IReadOnlyList<NewsItem>> ListAsync(Guid watchItemId, int days, bool unreadOnly, CancellationToken ct);

    /// <summary>
    /// Mark a news item as read or unread
    /// </summary>
    /// <param name="newsId">ID of the news item</param>
    /// <param name="isRead">Whether the item should be marked as read</param>
    /// <param name="ct">Cancellation token</param>
    Task MarkReadAsync(Guid newsId, bool isRead, CancellationToken ct);
}
