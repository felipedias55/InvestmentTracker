using InvestmentTracker.Application.History.Dtos;

namespace InvestmentTracker.Application.History.Interfaces
{
    public interface IHistoryService
    {
        Task<HistoryDto?> GetAsync(int portfolioId, CancellationToken cancellationToken = default);
        Task<SnapshotDetailDto?> GetSnapshotAsync(int portfolioId, int id, CancellationToken cancellationToken = default);
        Task<SnapshotDetailDto?> CaptureAsync(int portfolioId, bool replace, CancellationToken cancellationToken = default);
        Task<int?> CreateCashFlowAsync(int portfolioId, SaveCashFlowDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateCashFlowAsync(int portfolioId, int id, SaveCashFlowDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteCashFlowAsync(int portfolioId, int id, CancellationToken cancellationToken = default);
    }
}
