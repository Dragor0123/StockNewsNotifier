namespace StockNewsNotifier.Data.Entities;

/// <summary>
/// Many-to-many relationship between WatchItems and Sources
/// </summary>
public class WatchItemSource
{
    public Guid WatchItemId { get; set; }
    public int SourceId { get; set; }

    /// <summary>
    /// Custom query string for this watch item + source combination (optional)
    /// </summary>
    public string? CustomQuery { get; set; }

    /// <summary>
    /// Whether this source is enabled for this specific watch item
    /// </summary>
    public bool Enabled { get; set; } = true;

    // Navigation properties
    public WatchItem WatchItem { get; set; } = null!;
    public Source Source { get; set; } = null!;
}
