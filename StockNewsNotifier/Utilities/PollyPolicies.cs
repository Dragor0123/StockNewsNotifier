using System.Net.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace StockNewsNotifier.Utilities;

/// <summary>
/// Polly resilience policies for HTTP requests
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Get retry policy for HTTP requests with exponential backoff and jitter
    /// </summary>
    /// <param name="logger">Optional logger for logging retries</param>
    /// <returns>Async retry pipeline</returns>
    public static ResiliencePipeline<HttpResponseMessage> GetRetryPolicy(ILogger? logger = null)
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                // Retry up to 3 times
                MaxRetryAttempts = 3,

                // Exponential backoff: 2^retry seconds with jitter
                // Attempt 1: ~2 seconds
                // Attempt 2: ~4 seconds
                // Attempt 3: ~8 seconds
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                UseJitter = true,

                // Determine which responses should trigger a retry
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(response => !response.IsSuccessStatusCode &&
                                             (int)response.StatusCode >= 500), // Retry on 5xx errors

                // Log retry attempts
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        "Retry attempt {AttemptNumber} after {Delay}ms due to {Outcome}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString() ?? "unknown");

                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Get HttpClient timeout (10-15 seconds)
    /// </summary>
    public static TimeSpan GetHttpClientTimeout()
    {
        // Random timeout between 10-15 seconds to add variety
        return TimeSpan.FromSeconds(Random.Shared.Next(10, 16));
    }
}
