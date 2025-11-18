using Microsoft.Extensions.Logging;
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services.Interfaces;

namespace StockNewsNotifier.Services;

/// <summary>
/// Placeholder notification service that logs notifications.
/// This will be replaced with Windows toast notifications in a later phase.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(NewsItem item, CancellationToken ct)
    {
        var ticker = item.WatchItem?.Ticker ?? item.WatchItemId.ToString();
        _logger.LogInformation("Notification: {Ticker} - {Title}", ticker, item.Title);
        return Task.CompletedTask;
    }
}
