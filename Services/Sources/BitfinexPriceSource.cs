using System.Text.Json;

namespace BtcPriceService.Services.Sources;

public sealed class BitfinexPriceSource : IPriceSource
{
    private readonly HttpClient _http;

    public string Name => "Bitfinex";

    public BitfinexPriceSource(HttpClient http)
    {
        _http = http;
    }

    public async Task<double> GetCloseAtHourAsync(string instrument, DateTimeOffset timestampUtc, CancellationToken ct)
    {
        // Bitfinex uses "tBTCUSD"
        var sym = "t" + instrument.Replace("/", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();

        var endMs = timestampUtc.ToUnixTimeMilliseconds();
        var startMs = endMs - 3600_000;

        var url = $"/v2/candles/trade:1h:{sym}/hist?start={startMs}&end={endMs}&limit=1&sort=1";

        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 1)
            throw new InvalidOperationException("Bitfinex returned no candle data.");

        var candle = doc.RootElement[0];
        if (candle.ValueKind != JsonValueKind.Array || candle.GetArrayLength() < 6)
            throw new InvalidOperationException("Bitfinex candle format unexpected.");

        // index 2 = close action
        var close = candle[2].GetDouble();
        return close;
    }
}