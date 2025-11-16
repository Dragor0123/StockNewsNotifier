namespace StockNewsNotifier.Data.Entities;

/// <summary>
/// Represents a news source (Yahoo Finance, Reuters, etc.)
/// </summary>
public class Source
{
    public int Id { get; set; }

    /// <summary>
    /// Source name (e.g., "YahooFinance", "Reuters")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base URL/host for the source
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether this source is enabled for crawling
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Display name for UI
    /// </summary>
    public string? DisplayName { get; set; }

    // Navigation properties
    public ICollection<WatchItemSource> WatchItemSources { get; set; } = new List<WatchItemSource>();
    public ICollection<NewsItem> NewsItems { get; set; } = new List<NewsItem>();
    public CrawlState? CrawlState { get; set; }
}
