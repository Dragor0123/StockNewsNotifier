using System.Web;

namespace StockNewsNotifier.Utilities;

/// <summary>
/// Helper class for canonicalizing URLs by removing tracking parameters
/// </summary>
public static class UrlCanonicalizer
{
    // TODO: Move this to appsettings.json in a later phase for configurability
    private static readonly HashSet<string> TrackingParams = new(StringComparer.OrdinalIgnoreCase)
    {
        // UTM parameters (Google Analytics)
        "utm_source",
        "utm_medium",
        "utm_campaign",
        "utm_term",
        "utm_content",
        "utm_id",

        // Ad platform click IDs
        "gclid",      // Google Ads
        "fbclid",     // Facebook
        "msclkid",    // Microsoft Ads
        "yclid",      // Yahoo Ads

        // Email marketing
        "mc_cid",     // Mailchimp campaign ID
        "mc_eid",     // Mailchimp email ID

        // Other common tracking params
        "ref",
        "src"
    };

    /// <summary>
    /// Canonicalize a URL by removing tracking parameters and normalizing format
    /// </summary>
    /// <param name="url">URL to canonicalize</param>
    /// <returns>Canonicalized URL</returns>
    public static string Canonicalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url; // Return as-is if not a valid absolute URL

        // Parse query string
        var builder = new UriBuilder(uri);
        var query = HttpUtility.ParseQueryString(builder.Query);

        // Remove tracking parameters
        foreach (var param in TrackingParams)
        {
            query.Remove(param);
        }

        // Rebuild query string
        builder.Query = query.ToString();

        // Return canonicalized URL
        return builder.Uri.ToString();
    }
}
