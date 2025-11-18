using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StockNewsNotifier.Services.Interfaces;

namespace StockNewsNotifier.Services;

/// <summary>
/// Simple channel-based scheduler that queues crawl requests for watch items.
/// </summary>
public class ChannelScheduler : IScheduler
{
    private readonly Channel<Guid> _channel;
    private readonly ILogger<ChannelScheduler> _logger;

    public ChannelScheduler(ILogger<ChannelScheduler> logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Reader exposed so the background service can process queued watch item IDs.
    /// </summary>
    public ChannelReader<Guid> Reader => _channel.Reader;

    /// <inheritdoc />
    public void EnqueueCrawl(Guid watchItemId)
    {
        if (!_channel.Writer.TryWrite(watchItemId))
        {
            _logger.LogWarning("Failed to enqueue crawl job for watch item {WatchItemId}", watchItemId);
        }
        else
        {
            _logger.LogTrace("Enqueued crawl job for {WatchItemId}", watchItemId);
        }
    }
}
