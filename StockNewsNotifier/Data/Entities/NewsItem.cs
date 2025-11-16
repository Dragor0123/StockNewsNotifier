namespace StockNewsNotifier.Data.Entities;

/// <summary>
/// Represents a crawled news article
/// </summary>
public class NewsItem
{
    public Guid Id { get; set; }

    public Guid WatchItemId { get; set; }
    public int SourceId { get; set; }

    /// <summary>
    /// Article title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Original URL from the source
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Canonicalized URL (tracking params stripped)
    /// </summary>
    public string CanonicalUrl { get; set; } = string.Empty;

    /// <summary>
    /// Article summary/snippet (optional)
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// SHA256 hash of normalized title for deduplication
    /// </summary>
    public string TitleHash { get; set; } = string.Empty;

    /// <summary>
    /// SimHash for near-duplicate detection
    /// </summary>
    public long SimHash64 { get; set; }

    /// <summary>
    /// When the article was published (if available)
    /// </summary>
    public DateTime? PublishedUtc { get; set; }

    /// <summary>
    /// When we fetched/crawled this article
    /// </summary>
    public DateTime FetchedUtc { get; set; }

    /// <summary>
    /// Whether the user has marked this as read
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Whether we've sent a notification for this item
    /// </summary>
    public bool NotificationSent { get; set; }

    // Navigation properties
    public WatchItem WatchItem { get; set; } = null!;
    public Source Source { get; set; } = null!;
}
