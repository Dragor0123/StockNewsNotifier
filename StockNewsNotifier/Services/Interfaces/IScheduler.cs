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

    /// <summary>
    /// Mark a crawl job as completed so it can be enqueued again
    /// </summary>
    /// <param name="watchItemId">ID of the processed watch item</param>
    void MarkCompleted(Guid watchItemId);
}
