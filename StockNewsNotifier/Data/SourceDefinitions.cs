using System.Collections.Generic;

namespace StockNewsNotifier.Data;

public static class SourceDefinitions
{
    public static IReadOnlyList<SourceSeed> Defaults { get; } = new[]
    {
        new SourceSeed("YahooFinance", "Yahoo Finance", "https://finance.yahoo.com"),
        new SourceSeed("Reuters", "Reuters", "https://www.reuters.com/"),
        new SourceSeed("GoogleFinance", "Google Finance", "https://www.google.com/finance/"),
        new SourceSeed("Investing", "Investing.com", "https://www.investing.com/"),
        new SourceSeed("WSJ", "Wall Street Journal", "https://www.wsj.com/")
    };

    public record SourceSeed(string Name, string DisplayName, string BaseUrl);
}
