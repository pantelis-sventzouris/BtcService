using BtcPriceService.Data;
using BtcPriceService.Domain;
using BtcPriceService.Services.Aggregation;
using BtcPriceService.Services.Sources;
using Microsoft.EntityFrameworkCore;

namespace BtcPriceService.Services;

public sealed class PricingService
{
    private readonly PriceDbContext _db;
    private readonly IEnumerable<IPriceSource> _sources;
    private readonly IPriceAggregator _aggregator;

    public PricingService(PriceDbContext db, IEnumerable<IPriceSource> sources, IPriceAggregator aggregator)
    {
        _db = db;
        _sources = sources;
        _aggregator = aggregator;
    }

    public static DateTimeOffset NormalizeHourUtc(DateTimeOffset ts)
    {
        // Convert to UTC, then truncate to hour
        var utc = ts.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    public static void EnsureHourAccurate(DateTimeOffset ts)
    {
        var utc = ts.ToUniversalTime();
        if (utc.Minute != 0 || utc.Second != 0 || utc.Millisecond != 0)
            throw new ArgumentException("Timestamp must have hour accuracy (no minutes/seconds/milliseconds). Use e.g. 2023-01-01T01:00:00Z");
    }

    public async Task<PricePoint> GetOrCreateAggregatedAsync(string instrument, DateTimeOffset timestampUtc, CancellationToken ct)
    {
        // stored timestamps are normalized hour-utc
        timestampUtc = NormalizeHourUtc(timestampUtc);

        var existing = await _db.PricePoints
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Instrument == instrument && p.TimestampUtc == timestampUtc, ct);

        if (existing is not null)
            return existing;

        // Fetch in parallel
		// Accept partial failures
        var warnings = new List<string>();
        var tasks = _sources.Select(async s =>
        {
            try
            {
                var close = await s.GetCloseAtHourAsync(instrument, timestampUtc, ct);
                return (ok: true, src: s.Name, close, err: (string?)null);
            }
            catch (Exception ex)
            {
                return (ok: false, src: s.Name, close: 0d, err: ex.Message);
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var r in results.Where(r => !r.ok))
            warnings.Add($"{r.src} failed: {r.err}");

        var okPrices = results
            .Where(r => r.ok)
            .Select(r => new SourcePrice(r.src, r.close))
            .ToList();

        if (okPrices.Count == 0)
            throw new InvalidOperationException("All external sources failed; cannot compute aggregated price.");

        var (agg, used) = _aggregator.Aggregate(okPrices);

        // Persist (with unique constraint on Instrument+TimestampUtc)
        var entity = new PricePoint
        {
            Instrument = instrument,
            TimestampUtc = timestampUtc,
            AggregatedClose = agg,
            BitstampClose = used.FirstOrDefault(u => u.Source == "Bitstamp")?.Close,
            BitfinexClose = used.FirstOrDefault(u => u.Source == "Bitfinex")?.Close,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _db.PricePoints.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // race condition: another request wrote it first
            var raced = await _db.PricePoints
                .AsNoTracking()
                .SingleAsync(p => p.Instrument == instrument && p.TimestampUtc == timestampUtc, ct);
            return raced;
        }

        return entity;
    }

    public async Task<List<PricePoint>> GetRangeAsync(string instrument, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        fromUtc = NormalizeHourUtc(fromUtc);
        toUtc = NormalizeHourUtc(toUtc);

        if (toUtc < fromUtc)
            throw new ArgumentException("'to' must be >= 'from'.");

        return await _db.PricePoints
            .AsNoTracking()
            .Where(p => p.Instrument == instrument && p.TimestampUtc >= fromUtc && p.TimestampUtc <= toUtc)
            .OrderBy(p => p.TimestampUtc)
            .ToListAsync(ct);
    }
}