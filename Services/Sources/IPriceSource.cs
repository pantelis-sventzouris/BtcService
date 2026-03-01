namespace BtcPriceService.Services.Sources;

public interface IPriceSource
{
    string Name { get; }

    // Returns the CLOSE price for the candle ending at timestampUtc (hour-accurate).
    Task<double> GetCloseAtHourAsync(string instrument, DateTimeOffset timestampUtc, CancellationToken ct);
}