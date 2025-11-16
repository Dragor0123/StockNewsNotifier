namespace StockNewsNotifier.Data.Entities;

/// <summary>
/// Tracks crawl state, rate limits, and robots.txt cache for each source
/// </summary>
public class CrawlState
{
    public int SourceId { get; set; }

    /// <summary>
    /// Last time we crawled this source
    /// </summary>
    public DateTime? LastCrawlUtc { get; set; }

    /// <summary>
    /// Requests per second limit for this source
    /// </summary>
    public double RequestsPerSecond { get; set; } = 1.0;

    /// <summary>
    /// Requests per minute limit for this source
    /// </summary>
    public int RequestsPerMinute { get; set; } = 10;

    /// <summary>
    /// Cached robots.txt content
    /// </summary>
    public string? RobotsTxt { get; set; }

    /// <summary>
    /// When robots.txt was last fetched
    /// </summary>
    public DateTime? RobotsTxtFetchedUtc { get; set; }

    /// <summary>
    /// Number of consecutive errors encountered
    /// </summary>
    public int ConsecutiveErrors { get; set; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When the last error occurred
    /// </summary>
    public DateTime? LastErrorUtc { get; set; }

    // Navigation properties
    public Source Source { get; set; } = null!;
}
