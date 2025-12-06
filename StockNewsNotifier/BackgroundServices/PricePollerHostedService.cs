using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StockNewsNotifier.BackgroundServices;

/// <summary>
/// Placeholder price polling loop that uses its own interval settings.
/// Actual price retrieval will be added later.
/// </summary>
public class PricePollerHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PricePollerHostedService> _logger;

    public PricePollerHostedService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<PricePollerHostedService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseInterval = Math.Max(30, _configuration.GetValue<int>("PricePolling:DefaultIntervalSeconds", 300));
        var jitter = Math.Max(0, _configuration.GetValue<int>("PricePolling:JitterSeconds", 30));

        _logger.LogInformation("Price poller started with base interval {IntervalSeconds}s (jitter {JitterSeconds}s)",
            baseInterval, jitter);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // TODO: implement price retrieval and persistence

                var delay = CalculateDelay(baseInterval, jitter);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during price polling loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Price poller stopped");
    }

    private static TimeSpan CalculateDelay(int baseIntervalSeconds, int jitterSeconds)
    {
        var jitterOffset = jitterSeconds == 0 ? 0 : Random.Shared.Next(-jitterSeconds, jitterSeconds + 1);
        var delaySeconds = Math.Max(10, baseIntervalSeconds + jitterOffset);
        return TimeSpan.FromSeconds(delaySeconds);
    }
}
