using System.Text.Json.Serialization;
using InvestmentTracker.Application.Portfolios.Dtos;

namespace InvestmentTracker.Application.ExternalAssets.Dtos
{
    public sealed record ExternalAssetSummaryDto(
        PortfolioDto Portfolio,
        IReadOnlyList<ExternalAssetDto> Items,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? TotalValue,
        bool ConversionAvailable,
        bool HasStaleRates,
        bool HasFallbackRates);
}
