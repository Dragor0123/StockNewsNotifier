using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services.Crawlers;

namespace StockNewsNotifier.Tests.Crawlers;

/// <summary>
/// Exercises YahooFinanceCrawler.FetchAsync end-to-end using a captured HTML fixture and a fake HttpClientFactory.
/// Ensures BuildQueryUrls + FetchAsync integration works without hitting the network.
/// </summary>
internal static class YahooFinanceCrawlerHttpSmokeTest
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

            var handler = new FixtureHttpMessageHandler(fixturePath);
            var client = new HttpClient(handler);
            var factory = new SingleClientFactory(client);
            var logger = new ConsoleLogger<YahooFinanceCrawler>();

            var crawler = new YahooFinanceCrawler(factory, logger);
            var watchItem = new WatchItem { Id = Guid.NewGuid(), Ticker = "MSFT" };

            var urls = crawler.BuildQueryUrls(watchItem);
            if (urls.Count != 1 || !urls[0].Equals("https://finance.yahoo.com/quote/MSFT/news?p=MSFT", StringComparison.Ordinal))
            {
                errorMessage = $"Unexpected query URL: {string.Join(", ", urls)}";
                return false;
            }

            var articles = crawler.FetchAsync(urls[0], CancellationToken.None).GetAwaiter().GetResult();
            if (articles.Count != 2)
            {
                errorMessage = $"Expected 2 articles but fetched {articles.Count}.";
                return false;
            }

            if (!string.Equals(articles[0].Title, "Microsoft jumps after earnings beat", StringComparison.Ordinal))
            {
                errorMessage = "First article title mismatch.";
                return false;
            }

            if (!string.Equals(articles[1].Title, "Azure growth accelerates again", StringComparison.Ordinal))
            {
                errorMessage = "Second article title mismatch.";
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
