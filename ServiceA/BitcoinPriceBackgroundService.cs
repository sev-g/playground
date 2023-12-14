using Microsoft.Extensions.Options;
using ServiceA.Configuration;
using ServiceA.Services;

namespace ServiceA;

public class BitcoinPriceBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BitcoinPriceBackgroundService> _logger;

    public BitcoinPriceBackgroundService(IServiceProvider services, ILogger<BitcoinPriceBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // When the timer should have no due-time, then do the work once now.
        await DoWorkAsync();

        using var scope = _services.CreateScope();

        var config = scope.ServiceProvider.GetRequiredService<IOptions<BitcoinPriceBackgroundServiceConfig>>().Value;

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(config.IntervalInSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DoWorkAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BitcoinBackgroundService is stopping.");
        }
    }

    private async Task DoWorkAsync()
    {
        using var scope = _services.CreateScope();

        var bitcoinPriceApiService = scope.ServiceProvider.GetRequiredService<IBitcoinPriceApiClient>();
        var bitcoinPriceAggregator = scope.ServiceProvider.GetRequiredService<BitcoinPriceAggregator>();

        var price = await bitcoinPriceApiService.GetPriceAsync();

        bitcoinPriceAggregator.AddPrice(price);
    }
}
