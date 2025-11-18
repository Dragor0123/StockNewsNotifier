using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Polly;
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
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;
    private static readonly Uri YahooReferrer = new("https://finance.yahoo.com/");
    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public YahooFinanceCrawler(IHttpClientFactory httpClientFactory, ILogger<YahooFinanceCrawler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _retryPipeline = PollyPolicies.GetRetryPolicy(logger);
    }

    public string Name => "YahooFinance";
    public string BaseHost => "finance.yahoo.com";

    /// <inheritdoc/>
    public IReadOnlyList<string> BuildQueryUrls(WatchItem watch)
    {
        var symbol = (watch.Ticker ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(symbol))
        {
            _logger.LogWarning("Cannot build Yahoo Finance URL because ticker was blank for watch item {WatchItemId}", watch.Id);
            return Array.Empty<string>();
        }

        var encodedSymbol = Uri.EscapeDataString(symbol);
        var url = $"https://finance.yahoo.com/quote/{encodedSymbol}/news?p={encodedSymbol}";
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
            using var response = await _retryPipeline.ExecuteAsync(async token =>
            {
                using var request = BuildRequestMessage(url);
                return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            }, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Yahoo Finance returned 404 for {Url}. The server may be blocking non-browser requests.", url);
            }

            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(ct);

            // DEBUG: Save HTML to file for inspection
            var debugPath = Path.Combine(Path.GetTempPath(), "yahoo_finance_debug.html");
            await File.WriteAllTextAsync(debugPath, html, ct);
            _logger.LogInformation("Saved HTML to {Path} for debugging", debugPath);

            var articles = YahooFinanceHtmlParser.Parse(html, DateTime.UtcNow, _logger);

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
    /// Build an HttpRequestMessage with browser-like headers so Yahoo Finance is less likely to block the request
    /// </summary>
    private static HttpRequestMessage BuildRequestMessage(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = YahooReferrer;
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
        request.Headers.Pragma.ParseAdd("no-cache");
        request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.UserAgent.ParseAdd(DefaultUserAgent);
        return request;
    }
}
