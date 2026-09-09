using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.ExternalAssets.Dtos
{
    public sealed record ExternalAssetDto(
        int Id,
        string Name,
        int CurrencyId,
        string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Value,
        string? Description,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? BaseValue,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? ExchangeRate,
        DateOnly? RateDate,
        bool IsStale,
        bool IsFallback)
    {
        public DateOnly? UpdatedOn { get; init; }
    }
}
