namespace ServiceA.Configuration;

public class BitcoinPriceBackgroundServiceConfig
{
    public const string BitcoinPriceBackgroundServiceConfigSection = "BitcoinPriceBackgroundServiceConfig";

    public required int IntervalInSeconds { get; init; }
}