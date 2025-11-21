using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

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
        if (!_inFlight.TryAdd(watchItemId, 0))
        {
            _logger.LogTrace("Watch item {WatchItemId} already queued; skipping duplicate enqueue", watchItemId);
            return;
        }

        if (!_channel.Writer.TryWrite(watchItemId))
        {
            _logger.LogWarning("Failed to enqueue crawl job for watch item {WatchItemId}", watchItemId);
            _inFlight.TryRemove(watchItemId, out _);
        }
        else
        {
            _logger.LogTrace("Enqueued crawl job for {WatchItemId}", watchItemId);
        }
    }

    /// <inheritdoc />
    public void MarkCompleted(Guid watchItemId)
    {
        _inFlight.TryRemove(watchItemId, out _);
    }
}
