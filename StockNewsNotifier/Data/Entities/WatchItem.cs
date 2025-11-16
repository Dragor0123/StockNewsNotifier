namespace StockNewsNotifier.Data.Entities;

/// <summary>
/// Represents a watched stock ticker
/// </summary>
public class WatchItem
{
    public Guid Id { get; set; }

    /// <summary>
    /// Exchange code (e.g., "NASDAQ", "NYSE")
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// Stock ticker symbol (e.g., "MSFT", "AAPL")
    /// </summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// Company name for display
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// URL to company icon/logo
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Whether alerts are enabled for this ticker
    /// </summary>
    public bool AlertsEnabled { get; set; }

    /// <summary>
    /// When this watch item was created
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    // Navigation properties
    public ICollection<WatchItemSource> WatchItemSources { get; set; } = new List<WatchItemSource>();
    public ICollection<NewsItem> NewsItems { get; set; } = new List<NewsItem>();
}
