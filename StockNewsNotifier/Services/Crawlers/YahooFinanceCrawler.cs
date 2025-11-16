using System.Net.Http;
using AngleSharp;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using StockNewsNotifier.Data.Entities;
using StockNewsNotifier.Services.Interfaces;
using StockNewsNotifier.Utilities;

namespace StockNewsNotifier.Services.Crawlers;

/// <summary>
/// Crawler for Yahoo Finance news
/// </summary>
public class YahooFinanceCrawler : ISourceCrawler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YahooFinanceCrawler> _logger;

    public YahooFinanceCrawler(IHttpClientFactory httpClientFactory, ILogger<YahooFinanceCrawler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "YahooFinance";
    public string BaseHost => "finance.yahoo.com";

    /// <inheritdoc/>
    public IReadOnlyList<string> BuildQueryUrls(WatchItem watch)
    {
        // Yahoo Finance news URL format: https://finance.yahoo.com/quote/{TICKER}/news
        var url = $"https://finance.yahoo.com/quote/{watch.Ticker}/news";
        _logger.LogDebug("Built Yahoo Finance URL: {Url}", url);
        return new[] { url };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RawArticle>> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Fetching Yahoo Finance news from {Url}", url);

            var client = _httpClientFactory.CreateClient("crawler");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.Timeout = PollyPolicies.GetHttpClientTimeout();

            var html = await client.GetStringAsync(url, ct);
            var articles = ParseNewsPage(html);

            _logger.LogInformation("Fetched {Count} articles from Yahoo Finance", articles.Count);
            return articles;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching from {Url}", url);
            return Array.Empty<RawArticle>();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout fetching from {Url}", url);
            return Array.Empty<RawArticle>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching from {Url}", url);
            return Array.Empty<RawArticle>();
        }
    }

    /// <summary>
    /// Parse HTML to extract news articles
    /// </summary>
    private List<RawArticle> ParseNewsPage(string html)
    {
        var articles = new List<RawArticle>();

        try
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = context.OpenAsync(req => req.Content(html)).Result;

            // Find all news items using the data-testid attribute
            // Based on actual HTML: <section data-testid="storyitem">
            var newsItems = document.QuerySelectorAll("[data-testid='storyitem']");

            _logger.LogDebug("Found {Count} news items in HTML", newsItems.Length);

            foreach (var item in newsItems)
            {
                try
                {
                    // Find the title link (a tag with class "titles")
                    var titleLink = item.QuerySelector("a.titles");
                    if (titleLink == null)
                    {
                        _logger.LogDebug("No title link found in news item");
                        continue;
                    }

                    // Extract title from h3 tag inside the link
                    var titleElement = titleLink.QuerySelector("h3");
                    if (titleElement == null)
                    {
                        _logger.LogDebug("No h3 title found in link");
                        continue;
                    }

                    var title = titleElement.TextContent?.Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        _logger.LogDebug("Empty title found");
                        continue;
                    }

                    // Extract URL from href attribute
                    var url = titleLink.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        _logger.LogDebug("No URL found for article: {Title}", title);
                        continue;
                    }

                    // Make URL absolute if it's relative
                    if (url.StartsWith("/"))
                    {
                        url = $"https://finance.yahoo.com{url}";
                    }

                    // Extract publishing time from div.publishing
                    DateTime? publishedUtc = null;
                    var publishingDiv = item.QuerySelector("div.publishing");
                    if (publishingDiv != null)
                    {
                        var publishingText = publishingDiv.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(publishingText))
                        {
                            // Format: "Motley Fool • 33m ago"
                            // Extract the time part after the bullet point
                            var parts = publishingText.Split('•', StringSplitOptions.TrimEntries);
                            if (parts.Length > 1)
                            {
                                var timeString = parts[1].Trim();
                                publishedUtc = TimeParser.Parse(timeString, DateTime.UtcNow);
                                _logger.LogTrace("Parsed time '{TimeString}' as {PublishedUtc}", timeString, publishedUtc);
                            }
                        }
                    }

                    // Create RawArticle
                    var article = new RawArticle(
                        Title: title,
                        Url: url,
                        PublishedUtc: publishedUtc,
                        Summary: null // Yahoo Finance doesn't provide summaries in the list view
                    );

                    articles.Add(article);
                    _logger.LogTrace("Parsed article: {Title} - {Url}", title, url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse individual news item");
                    // Continue processing other items
                }
            }

            _logger.LogDebug("Successfully parsed {Count} articles", articles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML document");
        }

        return articles;
    }
}
