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
        bool IsFallback)
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        public decimal Income { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        public decimal? BaseIncome { get; init; }
        public DateOnly? UpdatedOn { get; init; }
        public int AssetTypeId { get; init; }
        public string AssetTypeName { get; init; } = string.Empty;
        public int AssetCategoryId { get; init; }
        public string AssetCategoryName { get; init; } = string.Empty;
        public int SectorId { get; init; }
        public string SectorName { get; init; } = string.Empty;
        public int CountryId { get; init; }
        public string CountryName { get; init; } = string.Empty;
    }
}
