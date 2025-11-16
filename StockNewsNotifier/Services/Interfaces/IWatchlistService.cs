using StockNewsNotifier.Data.Entities;

namespace StockNewsNotifier.Services.Interfaces;

/// <summary>
/// Service for managing watched stock tickers
/// </summary>
public interface IWatchlistService
{
    /// <summary>
    /// Add a new ticker to the watchlist
    /// </summary>
    /// <param name="ticker">Ticker to add</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created or existing WatchItem</returns>
    Task<WatchItem> AddAsync(Ticker ticker, CancellationToken ct = default);

    /// <summary>
    /// Remove a ticker from the watchlist
    /// </summary>
    /// <param name="watchItemId">ID of the watch item to remove</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveAsync(Guid watchItemId, CancellationToken ct = default);

    /// <summary>
    /// Enable or disable alerts for a specific ticker
    /// </summary>
    /// <param name="watchItemId">ID of the watch item</param>
    /// <param name="enabled">Whether alerts should be enabled</param>
    /// <param name="ct">Cancellation token</param>
    Task SetAlertsAsync(Guid watchItemId, bool enabled, CancellationToken ct = default);

    /// <summary>
    /// Get all watched tickers
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of all watch items</returns>
    Task<IReadOnlyList<WatchItem>> ListAsync(CancellationToken ct = default);
}
