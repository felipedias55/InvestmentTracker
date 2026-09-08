using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Portfolios.Interfaces
{
    public interface IPortfolioRepository
    {
        Task<IReadOnlyList<Portfolio>> GetAllAsync(CancellationToken cancellationToken);
        Task<Portfolio?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<IReadOnlyList<PortfolioAsset>> GetPositionsAsync(int portfolioId, CancellationToken cancellationToken);
        Task<PortfolioAsset?> GetPositionAsync(int portfolioId, int id, CancellationToken cancellationToken);
        Task<bool> HasAssetAsync(int portfolioId, int assetId, int? excludingId, CancellationToken cancellationToken);
        Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken);
        Task AddPositionAsync(PortfolioAsset position, CancellationToken cancellationToken);
        void RemovePosition(PortfolioAsset position);
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
