using BtcPriceService.Services.Sources;

namespace BtcPriceService.Services.Aggregation;

public sealed class AveragePriceAggregator : IPriceAggregator
{
    public string Name => "Average";

    public (double Aggregated, IReadOnlyList<SourcePrice> Used) Aggregate(IReadOnlyList<SourcePrice> prices)
    {
        if (prices.Count == 0)
            throw new InvalidOperationException("No prices to aggregate.");

        var avg = prices.Average(p => p.Close);
        return (avg, prices);
    }
}