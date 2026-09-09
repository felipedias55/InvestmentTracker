using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.ExternalAssets.Interfaces
{
    public interface IExternalAssetRepository
    {
        Task<IReadOnlyList<ExternalAsset>> GetAllAsync(int portfolioId, CancellationToken cancellationToken);
        Task<ExternalAsset?> GetByIdAsync(int portfolioId, int id, CancellationToken cancellationToken);
        Task AddAsync(ExternalAsset asset, CancellationToken cancellationToken);
        void Remove(ExternalAsset asset);
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
