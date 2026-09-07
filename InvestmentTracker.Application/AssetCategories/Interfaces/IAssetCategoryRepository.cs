using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.AssetCategories.Interfaces
{
    public interface IAssetCategoryRepository
    {
        Task<IReadOnlyList<AssetCategory>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<AssetCategory?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            string name,
            int? excludingId = null,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            AssetCategory assetCategory,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            AssetCategory assetCategory,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
