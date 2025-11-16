using System.Globalization;
using System.Text.RegularExpressions;

namespace StockNewsNotifier.Utilities;

/// <summary>
/// Helper class for parsing time strings from various sources
/// </summary>
public static partial class TimeParser
{
    // Regex for relative time patterns: "33m ago", "2h ago", "3d ago"
    [GeneratedRegex(@"(\d+)\s*(m|h|d|minute|hour|day)s?\s*ago", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeTimeRegex();

    /// <summary>
    /// Parse a relative time string (e.g., "33m ago", "2 hours ago")
    /// </summary>
    /// <param name="relativeTime">Relative time string</param>
    /// <param name="anchor">Anchor time to calculate from (typically DateTime.UtcNow)</param>
    /// <returns>Parsed UTC time, or null if parsing failed</returns>
    public static DateTime? ParseRelativeTime(string relativeTime, DateTime anchor)
    {
        if (string.IsNullOrWhiteSpace(relativeTime))
            return null;

        var match = RelativeTimeRegex().Match(relativeTime);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups[1].Value, out var value))
            return null;

        var unit = match.Groups[2].Value.ToLowerInvariant();

        // Map unit to time subtraction
        return unit[0] switch
        {
            'm' => anchor.AddMinutes(-value),
            'h' => anchor.AddHours(-value),
            'd' => anchor.AddDays(-value),
            _ => null
        };
    }

    /// <summary>
    /// Parse an absolute time string (ISO 8601 or common formats)
    /// </summary>
    /// <param name="dateString">Date/time string</param>
    /// <returns>Parsed UTC time, or null if parsing failed</returns>
    public static DateTime? ParseAbsoluteTime(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        // Try parsing with standard formats
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
        {
            return result;
        }

        // If parsing failed, return null
        // Special formats can be added here in future phases
        return null;
    }

    /// <summary>
    /// Try to parse any time string (relative or absolute)
    /// </summary>
    /// <param name="timeString">Time string to parse</param>
    /// <param name="anchor">Anchor time for relative parsing (defaults to UtcNow)</param>
    /// <returns>Parsed UTC time, or null if parsing failed</returns>
    public static DateTime? Parse(string timeString, DateTime? anchor = null)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            return null;

        var anchorTime = anchor ?? DateTime.UtcNow;

        // Try relative time first
        var relativeResult = ParseRelativeTime(timeString, anchorTime);
        if (relativeResult.HasValue)
            return relativeResult;

        // Fall back to absolute time
        return ParseAbsoluteTime(timeString);
    }
}
