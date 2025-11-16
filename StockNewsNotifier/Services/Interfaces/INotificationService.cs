using StockNewsNotifier.Data.Entities;

namespace StockNewsNotifier.Services.Interfaces;

/// <summary>
/// Service for sending notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send a notification for a news item
    /// </summary>
    /// <param name="item">News item to notify about</param>
    /// <param name="ct">Cancellation token</param>
    Task NotifyAsync(NewsItem item, CancellationToken ct);
}
