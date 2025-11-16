namespace StockNewsNotifier.Services.Interfaces;

/// <summary>
/// Represents a stock ticker with exchange and symbol
/// </summary>
/// <param name="Exchange">Exchange code (e.g., "NASDAQ", "NYSE")</param>
/// <param name="Symbol">Stock symbol (e.g., "MSFT", "AAPL")</param>
public record Ticker(string Exchange, string Symbol)
{
    public override string ToString() => $"{Exchange}:{Symbol}";
}
