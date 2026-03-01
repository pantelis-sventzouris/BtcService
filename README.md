# BTC Price Aggregation Microservice (.NET)

This service aggregates BTC/USD hourly close prices from:
- Bitstamp OHLC endpoint
- Bitfinex candles endpoint

It caches/persists aggregated prices and serves them from the datastore on repeat requests.

## Requirements
- .NET 6+ (project targets .NET 8 by default)
- Internet access (to fetch external prices)

## Run (SQLite default)
```bash
dotnet restore
dotnet run --project BtcPriceService