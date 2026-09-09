using InvestmentTracker.Application.ExternalAssets.Dtos;

namespace InvestmentTracker.Application.ExternalAssets.Interfaces
{
    public interface IExternalAssetService
    {
        Task<ExternalAssetSummaryDto?> GetSummaryAsync(int portfolioId, CancellationToken cancellationToken = default);
        Task<int?> CreateAsync(int portfolioId, SaveExternalAssetDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(int portfolioId, int id, SaveExternalAssetDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int portfolioId, int id, CancellationToken cancellationToken = default);
    }
}
