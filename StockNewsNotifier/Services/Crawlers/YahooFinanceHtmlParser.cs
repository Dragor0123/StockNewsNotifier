using System;
using System.Collections.Generic;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using StockNewsNotifier.Services.Interfaces;
using StockNewsNotifier.Utilities;

namespace StockNewsNotifier.Services.Crawlers;

/// <summary>
/// Parses Yahoo Finance news HTML into RawArticle records.
/// </summary>
internal static class YahooFinanceHtmlParser
{
    public static List<RawArticle> Parse(string html, DateTime? anchorTime = null, ILogger? logger = null)
    {
        var articles = new List<RawArticle>();

        if (string.IsNullOrWhiteSpace(html))
        {
            return articles;
        }

        try
        {
            var parser = new HtmlParser();
            var document = parser.ParseDocument(html);

            var newsItems = document.QuerySelectorAll("[data-testid='storyitem']");
            if (newsItems.Length == 0)
            {
                newsItems = document.QuerySelectorAll("li.js-stream-content");
            }

            logger?.LogDebug("Found {Count} news items in HTML", newsItems.Length);

            foreach (var item in newsItems)
            {
                TryParseArticle(item, anchorTime, logger, articles);
            }

            logger?.LogDebug("Successfully parsed {Count} articles", articles.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to parse Yahoo Finance HTML document");
        }

        return articles;
    }

    private static void TryParseArticle(
        IElement item,
        DateTime? anchorTime,
        ILogger? logger,
        ICollection<RawArticle> articles)
    {
        try
        {
            var titleLink = item.QuerySelector("a.titles") ??
                            item.QuerySelector("h3 a") ??
                            item.QuerySelector("a[data-ylk]") ??
                            item.QuerySelector("a");

            if (titleLink is not IHtmlAnchorElement anchor)
            {
                logger?.LogDebug("No title link found in Yahoo Finance news item");
                return;
            }

            var titleElement = titleLink.QuerySelector("h3") ?? anchor;
            var title = titleElement.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                logger?.LogDebug("Empty title found in Yahoo Finance news item");
                return;
            }

            var url = anchor.Href ?? anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(url))
            {
                logger?.LogDebug("No URL found for article: {Title}", title);
                return;
            }

            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                url = $"https:{url}";
            }
            else if (url.StartsWith("/", StringComparison.Ordinal))
            {
                url = $"https://finance.yahoo.com{url}";
            }

            DateTime? publishedUtc = null;
            var publishingDiv = item.QuerySelector("div.publishing");
            if (publishingDiv != null)
            {
                var publishingText = publishingDiv.TextContent?.Trim();
                if (!string.IsNullOrWhiteSpace(publishingText))
                {
                    var parts = publishingText.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        var timeString = parts[^1];
                        publishedUtc = TimeParser.Parse(timeString, anchorTime ?? DateTime.UtcNow);
                        logger?.LogTrace("Parsed time '{TimeString}' as {PublishedUtc}", timeString, publishedUtc);
                    }
                }
            }

            var article = new RawArticle(
                Title: title,
                Url: url,
                PublishedUtc: publishedUtc,
                Summary: null);

            articles.Add(article);
            logger?.LogTrace("Parsed article: {Title} - {Url}", title, url);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse individual Yahoo Finance news item");
        }
    }
}
