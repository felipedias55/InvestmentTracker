using InvestmentTracker.Application.AssetCategories.Dtos;

namespace InvestmentTracker.Application.AssetCategories.Interfaces
{
    public interface IAssetCategoryService
    {
        Task<IReadOnlyList<AssetCategoryDto>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<AssetCategoryDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<AssetCategoryDto> CreateAsync(
            CreateAssetCategoryDto dto,
            CancellationToken cancellationToken = default);

        Task<AssetCategoryDto?> UpdateAsync(
            int id,
            UpdateAssetCategoryDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
