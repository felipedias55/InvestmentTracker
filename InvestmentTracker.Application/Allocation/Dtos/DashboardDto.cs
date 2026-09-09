using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.ExternalAssets.Dtos;
using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record DashboardDto(
        PortfolioSummaryDto Summary,
        AllocationDto Allocation,
        ExternalAssetSummaryDto? ExternalAssets = null,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? TotalWealth = null);
}
