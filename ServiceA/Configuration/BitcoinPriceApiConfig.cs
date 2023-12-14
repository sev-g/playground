namespace ServiceA.Configuration;

public class BitcoinPriceApiConfig
{
    public const string BitcoinPriceApiConfigSection = "BitcoinPriceApiConfig";

    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string Scheme { get; init; }
}
