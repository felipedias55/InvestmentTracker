using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Portfolios.Dtos
{
    public sealed record UpdatePortfolioDto(
        string Name,
        string? Description,
        int BaseCurrencyId);
}
