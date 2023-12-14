namespace ServiceA.Services;

public interface IBitcoinPriceApiClient
{
    Task<BitcoinPrice> GetPriceAsync();
}
