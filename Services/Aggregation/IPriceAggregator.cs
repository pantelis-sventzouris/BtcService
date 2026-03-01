using BtcPriceService.Services.Sources;

namespace BtcPriceService.Services.Aggregation;

public interface IPriceAggregator
{
    string Name { get; }

    (double Aggregated, IReadOnlyList<SourcePrice> Used) Aggregate(IReadOnlyList<SourcePrice> prices);
}