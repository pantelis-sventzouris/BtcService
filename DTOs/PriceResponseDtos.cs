namespace BtcPriceService.Dtos;

public sealed record SourcePriceDto(string Source, double Close);

public sealed record AggregatedPriceDto(
    string Instrument,
    DateTimeOffset TimestampUtc,
    double AggregatedClose,
    IReadOnlyList<SourcePriceDto> SourcesUsed,
    IReadOnlyList<string> Warnings
);

public sealed record PersistedPriceDto(
    string Instrument,
    DateTimeOffset TimestampUtc,
    double AggregatedClose,
    double? BitstampClose,
    double? BitfinexClose
);