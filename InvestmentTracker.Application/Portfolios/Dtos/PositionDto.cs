using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Portfolios.Dtos
{
    public sealed record PositionDto(
        int Id,
        int AssetId,
        string Ticker,
        string Name,
        string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        decimal Quantity,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        decimal InvestedAmount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        decimal CurrentValue,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        decimal? BaseInvestedAmount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        decimal? BaseCurrentValue,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        decimal? ExchangeRate,
        DateOnly? RateDate,
        bool IsStale,
        bool IsFallback);
}
