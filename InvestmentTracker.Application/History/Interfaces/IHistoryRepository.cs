using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Application.History.Dtos;

namespace InvestmentTracker.Application.History.Interfaces
{
    public interface IHistoryRepository
    {
        Task<CashFlowPageDto> PageCashFlowsAsync(int portfolioId, CashFlowPageQuery query, CancellationToken cancellationToken);
        Task<IReadOnlyList<PortfolioSnapshot>> GetSnapshotsAsync(int portfolioId, CancellationToken cancellationToken);
        Task<PortfolioSnapshot?> GetSnapshotAsync(int portfolioId, int id, CancellationToken cancellationToken);
        Task<PortfolioSnapshot?> CaptureAsync(int portfolioId, bool replace, Func<CancellationToken, Task<PortfolioSnapshot>> create,
            CancellationToken cancellationToken);
        Task<IReadOnlyList<PortfolioCashFlow>> GetCashFlowsAsync(int portfolioId, CancellationToken cancellationToken);
        Task<PortfolioCashFlow?> GetCashFlowAsync(int portfolioId, int id, CancellationToken cancellationToken);
        Task AddCashFlowAsync(PortfolioCashFlow flow, CancellationToken cancellationToken);
        void RemoveCashFlow(PortfolioCashFlow flow);
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
