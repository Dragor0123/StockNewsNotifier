using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockNewsNotifier.Data;
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services.Interfaces;

namespace StockNewsNotifier.Services;

/// <summary>
/// Service for managing watched stock tickers
/// </summary>
public class WatchlistService : IWatchlistService
{
    private readonly AppDbContext _db;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(AppDbContext db, ILogger<WatchlistService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WatchItem> AddAsync(Ticker ticker, CancellationToken ct = default)
    {
        // Check if watch item already exists
        var existing = await _db.WatchItems
            .FirstOrDefaultAsync(w => w.Exchange == ticker.Exchange && w.Ticker == ticker.Symbol, ct);

        if (existing != null)
        {
            _logger.LogInformation("Watch item {Ticker} already exists", ticker);
            return existing;
        }

        // Create new watch item
        var watchItem = new WatchItem
        {
            Id = Guid.NewGuid(),
            Exchange = ticker.Exchange,
            Ticker = ticker.Symbol,
            AlertsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        };

        _db.WatchItems.Add(watchItem);

        // Add default source (YahooFinance) if it exists
        var yahooSource = await _db.Sources
            .FirstOrDefaultAsync(s => s.Name == "YahooFinance", ct);

        if (yahooSource == null)
        {
            var definition = SourceDefinitions.Defaults.First(s => s.Name == "YahooFinance");
            yahooSource = new Source
            {
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                BaseUrl = definition.BaseUrl,
                Enabled = true
            };
            _db.Sources.Add(yahooSource);
            await _db.SaveChangesAsync(ct);
        }

        _db.WatchItemSources.Add(new WatchItemSource
        {
            WatchItemId = watchItem.Id,
            SourceId = yahooSource.Id,
            Enabled = true
        });

        _logger.LogInformation("Added YahooFinance source to {Ticker}", ticker);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created new watch item for {Ticker}", ticker);
        return watchItem;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(Guid watchItemId, CancellationToken ct = default)
    {
        var item = await _db.WatchItems.FindAsync(new object[] { watchItemId }, ct);

        if (item == null)
        {
            _logger.LogWarning("Watch item {WatchItemId} not found for removal", watchItemId);
            return;
        }

        _db.WatchItems.Remove(item);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Removed watch item {WatchItemId} ({Exchange}:{Ticker})",
            watchItemId, item.Exchange, item.Ticker);
    }

    /// <inheritdoc/>
    public async Task SetAlertsAsync(Guid watchItemId, bool enabled, CancellationToken ct = default)
    {
        var item = await _db.WatchItems.FindAsync(new object[] { watchItemId }, ct);

        if (item == null)
        {
            _logger.LogWarning("Watch item {WatchItemId} not found for alert update", watchItemId);
            return;
        }

        item.AlertsEnabled = enabled;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Set alerts {Status} for {Exchange}:{Ticker}",
            enabled ? "enabled" : "disabled", item.Exchange, item.Ticker);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WatchItem>> ListAsync(CancellationToken ct = default)
    {
        var items = await _db.WatchItems
            .Include(w => w.WatchItemSources)
            .ThenInclude(ws => ws.Source)
            .OrderBy(w => w.Exchange)
            .ThenBy(w => w.Ticker)
            .ToListAsync(ct);

        _logger.LogDebug("Retrieved {Count} watch items", items.Count);
        return items;
    }
}
