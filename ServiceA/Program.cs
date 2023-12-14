using Polly;
using Polly.Extensions.Http;
using ServiceA;
using ServiceA.Configuration;
using ServiceA.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<BitcoinPriceApiConfig>(builder.Configuration.GetSection(BitcoinPriceApiConfig.BitcoinPriceApiConfigSection));
builder.Services.Configure<BitcoinPriceBackgroundServiceConfig>(builder.Configuration.GetSection(BitcoinPriceBackgroundServiceConfig.BitcoinPriceBackgroundServiceConfigSection));
// TODO - validate configuration

builder.Services.AddSingleton<BitcoinPriceAggregator>();

builder.Services.AddHttpClient<IBitcoinPriceApiClient, BitcoinPriceApiClient>() // alternatively the auth header can be injected here in the http client
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))  //Set lifetime to five minutes
        .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddHostedService<BitcoinPriceBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/v1/bitcoin/current", (BitcoinPriceAggregator aggregator) =>
{
    return aggregator.CurrentPrice ?? new BitcoinPrice(0, DateTimeOffset.UtcNow, "price not yet available");
})
.WithName("CurrentPrice")
.WithOpenApi();

app.MapGet("/api/v1/bitcoin/dataset", (BitcoinPriceAggregator aggregator) =>
{
    return aggregator.Dataset;
})
.WithName("Dataset")
.WithOpenApi();

app.MapGet("/api/v1/bitcoin/average", (BitcoinPriceAggregator aggregator) =>
{
    return aggregator.GetAveragePrice(TimeSpan.FromMinutes(10));
})
.WithName("AveragePrice")
.WithOpenApi();

app.MapGet("/api/v1/bitcoin/dummyPrice", (BitcoinPriceAggregator aggregator) =>
{
    return new Random().Next(10000, 11000);
})
.WithName("DummyPrice")
.WithOpenApi();

app.Run();
