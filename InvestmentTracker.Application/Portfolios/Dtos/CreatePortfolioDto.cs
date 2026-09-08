using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Portfolios.Dtos
{
    public sealed record CreatePortfolioDto(
        string Name,
        string? Description = null,
        int? BaseCurrencyId = null);
}
