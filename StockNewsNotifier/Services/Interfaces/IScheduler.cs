namespace StockNewsNotifier.Services.Interfaces;

/// <summary>
/// Scheduler for managing crawl jobs
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Enqueue a crawl job for a watch item
    /// </summary>
    /// <param name="watchItemId">ID of the watch item to crawl</param>
    void EnqueueCrawl(Guid watchItemId);
}
