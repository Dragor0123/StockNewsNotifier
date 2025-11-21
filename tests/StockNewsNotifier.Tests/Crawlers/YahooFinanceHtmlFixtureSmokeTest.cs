using System.Globalization;
using StockNewsNotifier.Services.Crawlers;

namespace StockNewsNotifier.Tests.Crawlers;

/// <summary>
/// Lightweight smoke tests that validate the Yahoo HTML parser against saved fixtures.
/// </summary>
internal static class YahooFinanceHtmlFixtureSmokeTest
{
    private sealed record ExpectedArticle(string Title, string Url, DateTime? PublishedUtc);

    private sealed record ParserFixtureCase(
        string Name,
        string FixtureFile,
        DateTime AnchorTime,
        ExpectedArticle[] Expectations);

    public static bool Run(out string? errorMessage)
    {
        try
        {
            var cases = new[]
            {
                new ParserFixtureCase(
                    Name: "msft-storyitem",
                    FixtureFile: "msft_news_sample.html",
                    AnchorTime: new DateTime(2024, 5, 4, 13, 0, 0, DateTimeKind.Utc),
                    Expectations: new[]
                    {
                        new ExpectedArticle(
                            "Microsoft jumps after earnings beat",
                            "https://finance.yahoo.com/news/sample-article-one.html",
                            DateTime.Parse("2024-05-04T12:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)),
                        new ExpectedArticle(
                            "Azure growth accelerates again",
                            "https://finance.yahoo.com/news/sample-article-two.html",
                            DateTime.Parse("2024-05-04T11:30:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal))
                    }),
                new ParserFixtureCase(
                    Name: "aapl-stream-content",
                    FixtureFile: "aapl_news_sample.html",
                    AnchorTime: new DateTime(2024, 5, 4, 13, 0, 0, DateTimeKind.Utc),
                    Expectations: new[]
                    {
                        new ExpectedArticle(
                            "Apple pushes new AI chips",
                            "https://finance.yahoo.com/news/sample-article-three.html",
                            new DateTime(2024, 5, 4, 12, 15, 0, DateTimeKind.Utc)),
                        new ExpectedArticle(
                            "Services revenue keeps climbing",
                            "https://finance.yahoo.com/news/sample-article-four.html",
                            new DateTime(2024, 5, 4, 11, 0, 0, DateTimeKind.Utc))
                    })
            };

            foreach (var testCase in cases)
            {
                var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "YahooFinance", testCase.FixtureFile);
                if (!File.Exists(fixturePath))
                {
                    errorMessage = $"Fixture not found at {fixturePath}";
                    return false;
                }

                var html = File.ReadAllText(fixturePath);
                var articles = YahooFinanceHtmlParser.Parse(html, testCase.AnchorTime);
                if (articles.Count != testCase.Expectations.Length)
                {
                    errorMessage = $"{testCase.Name}: Expected {testCase.Expectations.Length} articles but parsed {articles.Count}.";
                    return false;
                }

                for (var i = 0; i < testCase.Expectations.Length; i++)
                {
                    var expected = testCase.Expectations[i];
                    var actual = articles[i];

                    if (!string.Equals(expected.Title, actual.Title, StringComparison.Ordinal))
                    {
                        errorMessage = $"{testCase.Name}: Article {i} title mismatch. Expected '{expected.Title}' but got '{actual.Title}'.";
                        return false;
                    }

                    if (!string.Equals(expected.Url, actual.Url, StringComparison.Ordinal))
                    {
                        errorMessage = $"{testCase.Name}: Article {i} URL mismatch. Expected '{expected.Url}' but got '{actual.Url}'.";
                        return false;
                    }

                    if (expected.PublishedUtc != actual.PublishedUtc)
                    {
                        errorMessage = $"{testCase.Name}: Article {i} publish time mismatch.";
                        return false;
                    }
                }
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
