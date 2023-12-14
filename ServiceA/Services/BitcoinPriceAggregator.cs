namespace ServiceA.Services;

public class BitcoinPriceAggregator
{
    private const int _maxBufferSize = 100;

    private List<BitcoinPrice> _prices = new(_maxBufferSize);

    public IReadOnlyCollection<BitcoinPrice> Dataset => _prices.AsReadOnly();

    public BitcoinPrice? CurrentPrice { get; private set; }

    public void AddPrice(BitcoinPrice price)
    {
        // TODO - implement thread safe solution with channels

        if (_prices.Count > _maxBufferSize)
        {
            var obsoletePrice = _prices.MinBy(p => p.Timestamp);
            _prices.Remove(obsoletePrice!);
        }

        _prices.Add(price);

        CurrentPrice = price;
    }

    public BitcoinPrice GetAveragePrice(TimeSpan duration)
    {
        var now = DateTimeOffset.UtcNow;

        var start = now - duration;

        var prices = _prices.Where(p => p.Timestamp >= start && p.Timestamp <= now).ToList();

        var average = prices.Sum(p => p.Price) / prices.Count;

        return new(average, now, $"Average price interval {start.ToString("HH:mm:ss")} - {now.ToString("HH:mm:ss")}, dataset count {prices.Count}");
    }
}
