using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services.Crawlers;

namespace StockNewsNotifier.Tests.Crawlers;

/// <summary>
/// Exercises YahooFinanceCrawler.FetchAsync end-to-end using captured HTML fixtures and a fake HttpClientFactory.
/// Ensures BuildQueryUrls + FetchAsync integration works without hitting the network.
/// </summary>
internal static class YahooFinanceCrawlerHttpSmokeTest
{
    public static bool Run(out string? errorMessage)
    {
        try
        {
            var cases = new[]
            {
                new
                {
                    Name = "msft-storyitem",
                    FixtureFile = "msft_news_sample.html",
                    Ticker = "MSFT",
                    ExpectedTitles = new[]
                    {
                        "Microsoft jumps after earnings beat",
                        "Azure growth accelerates again"
                    },
                    ExpectedUrls = new[]
                    {
                        "https://finance.yahoo.com/news/sample-article-one.html",
                        "https://finance.yahoo.com/news/sample-article-two.html"
                    }
                },
                new
                {
                    Name = "aapl-stream-content",
                    FixtureFile = "aapl_news_sample.html",
                    Ticker = "AAPL",
                    ExpectedTitles = new[]
                    {
                        "Apple pushes new AI chips",
                        "Services revenue keeps climbing"
                    },
                    ExpectedUrls = new[]
                    {
                        "https://finance.yahoo.com/news/sample-article-three.html",
                        "https://finance.yahoo.com/news/sample-article-four.html"
                    }
                }
            };

            foreach (var testCase in cases)
            {
                var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "YahooFinance", testCase.FixtureFile);
                if (!File.Exists(fixturePath))
                {
                    errorMessage = $"{testCase.Name}: Fixture not found at {fixturePath}";
                    return false;
                }

                var handler = new FixtureHttpMessageHandler(fixturePath);
                var client = new HttpClient(handler);
                var factory = new SingleClientFactory(client);
                var logger = new ConsoleLogger<YahooFinanceCrawler>();

                var crawler = new YahooFinanceCrawler(factory, logger);
                var watchItem = new WatchItem { Id = Guid.NewGuid(), Ticker = testCase.Ticker };

                var urls = crawler.BuildQueryUrls(watchItem);
                var expectedUrl = $"https://finance.yahoo.com/quote/{testCase.Ticker}/news?p={testCase.Ticker}";
                if (urls.Count != 1 || !urls[0].Equals(expectedUrl, StringComparison.Ordinal))
                {
                    errorMessage = $"{testCase.Name}: Unexpected query URL: {string.Join(", ", urls)}";
                    return false;
                }

                var articles = crawler.FetchAsync(urls[0], CancellationToken.None).GetAwaiter().GetResult();
                if (articles.Count != testCase.ExpectedTitles.Length)
                {
                    errorMessage = $"{testCase.Name}: Expected {testCase.ExpectedTitles.Length} articles but fetched {articles.Count}.";
                    return false;
                }

                for (var i = 0; i < testCase.ExpectedTitles.Length; i++)
                {
                    var article = articles[i];
                    if (!string.Equals(testCase.ExpectedTitles[i], article.Title, StringComparison.Ordinal))
                    {
                        errorMessage = $"{testCase.Name}: Article {i} title mismatch.";
                        return false;
                    }

                    if (!string.Equals(testCase.ExpectedUrls[i], article.Url, StringComparison.Ordinal))
                    {
                        errorMessage = $"{testCase.Name}: Article {i} URL mismatch.";
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

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class FixtureHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _fixturePath;

        public FixtureHttpMessageHandler(string fixturePath)
        {
            _fixturePath = fixturePath;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var html = File.ReadAllText(_fixturePath);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return Task.FromResult(response);
        }
    }

    private sealed class ConsoleLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Console.WriteLine($"[{typeof(T).Name}] {logLevel}: {message}");
            if (exception != null)
            {
                Console.WriteLine(exception);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
