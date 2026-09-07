using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.AssetCategories.Dtos;
using InvestmentTracker.Application.AssetCategories.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.AssetCategories.Services
{
    public sealed class AssetCategoryService(
    IAssetCategoryRepository repository) : IAssetCategoryService
    {
        public async Task<IReadOnlyList<AssetCategoryDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var assetCategories = await repository.GetAllAsync(cancellationToken);

            return assetCategories
                .Select(MapToDto)
                .ToList();
        }

        public async Task<AssetCategoryDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var assetCategory = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return assetCategory is null
                ? null
                : MapToDto(assetCategory);
        }

        public async Task<AssetCategoryDto> CreateAsync(
            CreateAssetCategoryDto dto,
            CancellationToken cancellationToken = default)
        {
            var name = ValidateName(dto.Name);

            var exists = await repository.ExistsByNameAsync(
                name,
                cancellationToken: cancellationToken);

            if (exists)
            {
                throw new ResourceConflictException(
                    "Já existe uma categoria com esse nome.");
            }

            var assetCategory = new AssetCategory
            {
                Name = name
            };

            await repository.AddAsync(
                assetCategory,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(assetCategory);
        }

        public async Task<AssetCategoryDto?> UpdateAsync(
            int id,
            UpdateAssetCategoryDto dto,
            CancellationToken cancellationToken = default)
        {
            var name = ValidateName(dto.Name);

            var assetCategory = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (assetCategory is null)
            {
                return null;
            }

            var exists = await repository.ExistsByNameAsync(
                name,
                id,
                cancellationToken);

            if (exists)
            {
                throw new ResourceConflictException(
                    "Já existe uma categoria com esse nome.");
            }

            assetCategory.Name = name;

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(assetCategory);
        }

        public async Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var assetCategory = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (assetCategory is null)
            {
                return false;
            }

            await repository.DeleteAsync(
                assetCategory,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return true;
        }

        private static string ValidateName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InputValidationException(
                    "O nome da categoria é obrigatório.");
            }

            var name = value.Trim();
            if (name.Length > AssetCategory.NameMaxLength)
            {
                throw new InputValidationException(
                    $"O nome da categoria deve ter no máximo {AssetCategory.NameMaxLength} caracteres.");
            }

            return name;
        }

        private static AssetCategoryDto MapToDto(AssetCategory assetCategory)
        {
            return new AssetCategoryDto(
                assetCategory.Id,
                assetCategory.Name);
        }
    }
}
