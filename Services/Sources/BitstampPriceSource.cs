using System.Text.Json;

namespace BtcPriceService.Services.Sources;

public sealed class BitstampPriceSource : IPriceSource
{
    private readonly HttpClient _http;

    public string Name => "Bitstamp";

    public BitstampPriceSource(HttpClient http)
    {
        _http = http;
    }

    public async Task<double> GetCloseAtHourAsync(string instrument, DateTimeOffset timestampUtc, CancellationToken ct)
    {
        // Bitstamp uses lowercase pair without slash: "btcusd"
        var pair = instrument.Replace("/", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

        var endSec = timestampUtc.ToUnixTimeSeconds();
        var startSec = endSec - 3600;

        var url = $"/api/v2/ohlc/{pair}/?step=3600&limit=1&start={startSec}&end={endSec}&exclude_current_candle=true";

        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        // Response format:
        // { "data": { "ohlc": [ { "close": "xxxxx", ... } ] } }
        var ohlcArr = doc.RootElement
            .GetProperty("data")
            .GetProperty("ohlc");

        if (ohlcArr.GetArrayLength() < 1)
            throw new InvalidOperationException("Bitstamp returned no OHLC data.");

        var closeStr = ohlcArr[0].GetProperty("close").GetString();
        if (closeStr is null || !double.TryParse(closeStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var close))
        {
            throw new InvalidOperationException("Bitstamp close price missing/unparseable.");
        }

        return close;
    }
}