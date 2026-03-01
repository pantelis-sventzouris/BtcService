using System.ComponentModel.DataAnnotations;

namespace BtcPriceService.Domain;

public sealed class PricePoint
{
    [Key]
    public long Id { get; set; }

    // "BTC/USD" choice
    [MaxLength(16)]
    public string Instrument { get; set; } = "BTC/USD";

    // Hour-accurate UTC timestamp
    public DateTimeOffset TimestampUtc { get; set; }

    // Aggregated price close
    public double AggregatedClose { get; set; }

    // Optional source closes
    public double? BitstampClose { get; set; }
    public double? BitfinexClose { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}