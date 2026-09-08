using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Portfolios.Dtos
{
    public sealed record PortfolioSummaryDto(
        PortfolioDto Portfolio,
        IReadOnlyList<PositionDto> Positions,
        IReadOnlyList<CurrencySubtotalDto> OriginalSubtotals,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        decimal? TotalInvested,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        decimal? CurrentValue,
        bool ConversionAvailable,
        bool HasStaleRates,
        bool HasFallbackRates);
}
