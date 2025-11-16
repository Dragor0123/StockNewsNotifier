using StockNewsNotifier.Data.Entities;

namespace StockNewsNotifier.Services.Interfaces;

/// <summary>
/// Represents a raw article fetched from a news source
/// </summary>
/// <param name="Title">Article title</param>
/// <param name="Url">Article URL</param>
/// <param name="PublishedUtc">When the article was published (null if unknown)</param>
/// <param name="Summary">Article summary/snippet (optional)</param>
public record RawArticle(
    string Title,
    string Url,
    DateTime? PublishedUtc,
    string? Summary
);

/// <summary>
/// Interface for news source crawlers
/// </summary>
public interface ISourceCrawler
{
    /// <summary>
    /// Name of the crawler (e.g., "YahooFinance")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Base host for this source (e.g., "finance.yahoo.com")
    /// </summary>
    string BaseHost { get; }

    /// <summary>
    /// Build query URLs for a given watch item
    /// </summary>
    /// <param name="watch">Watch item to build URLs for</param>
    /// <returns>List of URLs to crawl</returns>
    IReadOnlyList<string> BuildQueryUrls(WatchItem watch);

    /// <summary>
    /// Fetch articles from a URL
    /// </summary>
    /// <param name="url">URL to fetch</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of raw articles found</returns>
    Task<IReadOnlyList<RawArticle>> FetchAsync(string url, CancellationToken ct);
}
