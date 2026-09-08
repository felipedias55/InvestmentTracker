using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Portfolios.Dtos
{
    public sealed record PortfolioDto(
        int Id,
        string Name,
        string? Description,
        int BaseCurrencyId,
        string BaseCurrencyCode);
}
