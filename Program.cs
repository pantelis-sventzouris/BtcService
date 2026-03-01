using BtcPriceService.Data;
using BtcPriceService.Dtos;
using BtcPriceService.Services;
using BtcPriceService.Services.Aggregation;
using BtcPriceService.Services.Sources;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";

builder.Services.AddDbContext<PriceDbContext>(opt =>
{
    if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
    {
        opt.UseInMemoryDatabase("prices");
    }
    else
    {
        var path = builder.Configuration["Database:SqlitePath"] ?? "prices.db";
        opt.UseSqlite($"Data Source={path}");
    }
});

// Aggregation
builder.Services.AddSingleton<IPriceAggregator, AveragePriceAggregator>();

// Http clients for sources
builder.Services.AddHttpClient<BitstampPriceSource>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["ExternalSources:BitstampBaseUrl"]!);
    http.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddHttpClient<BitfinexPriceSource>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["ExternalSources:BitfinexBaseUrl"]!);
    http.Timeout = TimeSpan.FromSeconds(8);
});

// Register sources via interface
builder.Services.AddScoped<IPriceSource>(sp => sp.GetRequiredService<BitstampPriceSource>());
builder.Services.AddScoped<IPriceSource>(sp => sp.GetRequiredService<BitfinexPriceSource>());

// Core service
builder.Services.AddScoped<PricingService>();

var app = builder.Build();

// Create DB on startup for prototype
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PriceDbContext>();
    if (db.Database.IsRelational())
        db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

// Health
app.MapGet("/health", () => Results.Ok(new { ok = true }));

const string Instrument = "BTC/USD";

// 1) Get aggregated BTC/USD at an hour-accurate timestamp
// Example: GET /api/prices/btcusd/2023-01-01T01:00:00Z
app.MapGet("/api/prices/btcusd/{timestamp}", async (
    string timestamp,
    PricingService pricing,
    CancellationToken ct) =>
{
    if (!DateTimeOffset.TryParse(timestamp, out var ts))
        return Results.BadRequest(new { error = "Invalid timestamp. Use ISO-8601 like 2023-01-01T01:00:00Z" });

    try
    {
        PricingService.EnsureHourAccurate(ts);
        var normalized = PricingService.NormalizeHourUtc(ts);

        var point = await pricing.GetOrCreateAggregatedAsync(Instrument, normalized, ct);

        var sources = new List<SourcePriceDto>();
        if (point.BitstampClose is not null) sources.Add(new SourcePriceDto("Bitstamp", point.BitstampClose.Value));
        if (point.BitfinexClose is not null) sources.Add(new SourcePriceDto("Bitfinex", point.BitfinexClose.Value));

        // We don’t persist warnings currently; warnings are only relevant on fresh fetch.
        // For a prototype, return empty warnings when served from cache.
        var dto = new AggregatedPriceDto(
            point.Instrument,
            point.TimestampUtc,
            point.AggregatedClose,
            sources,
            Array.Empty<string>()
        );

        return Results.Ok(dto);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 502);
    }
});

// 2) Get persisted BTC/USD in a range
// Example: GET /api/prices/btcusd?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z
app.MapGet("/api/prices/btcusd", async (
    string from,
    string to,
    PricingService pricing,
    CancellationToken ct) =>
{
    if (!DateTimeOffset.TryParse(from, out var fromTs))
        return Results.BadRequest(new { error = "Invalid 'from' timestamp." });
    if (!DateTimeOffset.TryParse(to, out var toTs))
        return Results.BadRequest(new { error = "Invalid 'to' timestamp." });

    try
    {
        PricingService.EnsureHourAccurate(fromTs);
        PricingService.EnsureHourAccurate(toTs);

        var list = await pricing.GetRangeAsync(Instrument, fromTs, toTs, ct);

        var dto = list.Select(p => new PersistedPriceDto(
            p.Instrument,
            p.TimestampUtc,
            p.AggregatedClose,
            p.BitstampClose,
            p.BitfinexClose
        )).ToList();

        return Results.Ok(dto);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();