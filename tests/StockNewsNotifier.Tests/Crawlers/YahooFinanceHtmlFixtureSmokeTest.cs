using System.Globalization;
using StockNewsNotifier.Services.Crawlers;

namespace StockNewsNotifier.Tests.Crawlers;

/// <summary>
/// Lightweight smoke test that validates the HTML parser against a saved Yahoo Finance fixture.
/// This avoids pulling in external test frameworks (network-restricted environment).
/// </summary>
internal static class YahooFinanceHtmlFixtureSmokeTest
{
    public static bool Run(out string? errorMessage)
    {
        try
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "YahooFinance", "msft_news_sample.html");
            if (!File.Exists(fixturePath))
            {
                errorMessage = $"Fixture not found at {fixturePath}";
                return false;
            }

            var html = File.ReadAllText(fixturePath);
            var anchorTime = new DateTime(2024, 5, 4, 13, 0, 0, DateTimeKind.Utc);

            var articles = YahooFinanceHtmlParser.Parse(html, anchorTime);
            if (articles.Count != 2)
            {
                errorMessage = $"Expected 2 articles but parsed {articles.Count}.";
                return false;
            }

            var first = articles[0];
            if (!string.Equals(first.Title, "Microsoft jumps after earnings beat", StringComparison.Ordinal))
            {
                errorMessage = "First article title mismatch.";
                return false;
            }

            if (!string.Equals(first.Url, "https://finance.yahoo.com/news/sample-article-one.html", StringComparison.Ordinal))
            {
                errorMessage = "First article URL mismatch.";
                return false;
            }

            var expectedFirstTime = DateTime.Parse("2024-05-04T12:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            if (first.PublishedUtc != expectedFirstTime)
            {
                errorMessage = "First article publish time mismatch.";
                return false;
            }

            var second = articles[1];
            if (!string.Equals(second.Title, "Azure growth accelerates again", StringComparison.Ordinal))
            {
                errorMessage = "Second article title mismatch.";
                return false;
            }

            if (!string.Equals(second.Url, "https://finance.yahoo.com/news/sample-article-two.html", StringComparison.Ordinal))
            {
                errorMessage = "Second article URL mismatch.";
                return false;
            }

            var expectedSecondTime = DateTime.Parse("2024-05-04T11:30:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            if (second.PublishedUtc != expectedSecondTime)
            {
                errorMessage = "Second article publish time mismatch.";
                return false;
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.ToString();
            return false;
        }
    }
}
