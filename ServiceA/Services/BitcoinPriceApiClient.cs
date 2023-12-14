using Microsoft.Extensions.Options;
using ServiceA.Configuration;
using System.Net.Http.Headers;

namespace ServiceA.Services;

public class BitcoinPriceApiClient : IBitcoinPriceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<BitcoinPriceApiConfig> _options;

    public BitcoinPriceApiClient(HttpClient httpClient, IOptions<BitcoinPriceApiConfig> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<BitcoinPrice> GetPriceAsync()
    {
        var config = _options.Value;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(config.Scheme, config.ApiKey);

        var response = await _httpClient.GetStringAsync(config.Endpoint);

        decimal price = Convert.ToDecimal(response);

        return new BitcoinPrice(price, DateTimeOffset.UtcNow, "from api");
    }
}
