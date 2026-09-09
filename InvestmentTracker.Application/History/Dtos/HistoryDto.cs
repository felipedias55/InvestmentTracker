using InvestmentTracker.Application.Portfolios.Dtos;

namespace InvestmentTracker.Application.History.Dtos
{
    public sealed record HistoryDto(
        PortfolioDto Portfolio,
        DateOnly Today,
        IReadOnlyList<HistoryPeriodDto> Months,
        IReadOnlyList<HistoryPeriodDto> Years,
        IReadOnlyList<CashFlowDto> CashFlows);
}
