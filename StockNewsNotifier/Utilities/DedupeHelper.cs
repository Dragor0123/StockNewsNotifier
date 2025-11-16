using System.Security.Cryptography;
using System.Text;

namespace StockNewsNotifier.Utilities;

/// <summary>
/// Helper class for deduplication of news articles
/// </summary>
public static class DedupeHelper
{
    /// <summary>
    /// Compute SHA256 hash of a normalized title for exact duplicate detection
    /// </summary>
    /// <param name="title">Article title</param>
    /// <returns>Hex-encoded SHA256 hash</returns>
    public static string ComputeTitleHash(string title)
    {
        var normalized = NormalizeTitle(title);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Compute SimHash for near-duplicate detection
    /// NOTE: MVP implementation - always returns 0
    /// TODO: Implement proper SimHash in later phase
    /// </summary>
    /// <param name="text">Text to hash</param>
    /// <returns>SimHash value (currently always 0)</returns>
    public static long ComputeSimHash(string text)
    {
        // MVP: SimHash not implemented yet
        // Will be implemented in a later phase for near-duplicate detection
        return 0;
    }

    /// <summary>
    /// Normalize title for consistent hashing
    /// </summary>
    /// <param name="title">Raw title</param>
    /// <returns>Normalized title</returns>
    private static string NormalizeTitle(string title)
    {
        // Convert to lowercase and trim whitespace
        return title.Trim().ToLowerInvariant();
    }
}
