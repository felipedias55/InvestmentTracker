using InvestmentTracker.Application.Assets.Dtos;
using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.Application.Countries.Interfaces;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.AssetCategories.Interfaces;
using InvestmentTracker.Application.Sectors.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Common.Validation;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Assets.Services
{
    public sealed class AssetService(
        IAssetRepository repository,
        IAssetTypeRepository assetTypes,
        ICountryRepository countries,
        ICurrencyRepository currencies,
        IAssetCategoryRepository categories,
        ISectorRepository sectors) : IAssetService
    {
        public async Task<IReadOnlyList<AssetDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var assets = await repository.GetAllAsync(cancellationToken);
            return assets.Select(MapToDto).ToList();
        }

        public async Task<AssetDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var asset = await repository.GetByIdAsync(id, cancellationToken);
            return asset is null ? null : MapToDto(asset);
        }

        public async Task<AssetDto> CreateAsync(CreateAssetDto dto, CancellationToken cancellationToken = default)
        {
            var ticker = InputRules.RequiredText(dto.Ticker, "O ticker", Asset.TickerMaxLength).ToUpperInvariant();
            var name = InputRules.RequiredText(dto.Name, "O nome do ativo", Asset.NameMaxLength);
            await ValidateReferencesAsync(dto.AssetTypeId, dto.CountryId, dto.CurrencyId,
                dto.AssetCategoryId, dto.SectorId, cancellationToken);
            await CheckDuplicateAsync(ticker, null, cancellationToken);

            var asset = new Asset
            {
                Ticker = ticker, Name = name, AssetTypeId = dto.AssetTypeId,
                CountryId = dto.CountryId, CurrencyId = dto.CurrencyId,
                AssetCategoryId = dto.AssetCategoryId, SectorId = dto.SectorId,
                CreatedAt = DateTime.UtcNow
            };
            await repository.AddAsync(asset, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return MapToDto(asset);
        }

        public async Task<AssetDto?> UpdateAsync(int id, UpdateAssetDto dto, CancellationToken cancellationToken = default)
        {
            var ticker = InputRules.RequiredText(dto.Ticker, "O ticker", Asset.TickerMaxLength).ToUpperInvariant();
            var name = InputRules.RequiredText(dto.Name, "O nome do ativo", Asset.NameMaxLength);
            var asset = await repository.GetByIdAsync(id, cancellationToken);
            if (asset is null)
            {
                return null;
            }

            await ValidateReferencesAsync(dto.AssetTypeId, dto.CountryId, dto.CurrencyId,
                dto.AssetCategoryId, dto.SectorId, cancellationToken);
            await CheckDuplicateAsync(ticker, id, cancellationToken);

            if (asset.CurrencyId != dto.CurrencyId && await repository.HasPositionsAsync(id, cancellationToken))
                throw new ResourceConflictException("A moeda de um ativo com posições não pode ser alterada.");

            asset.Ticker = ticker;
            asset.Name = name;
            asset.AssetTypeId = dto.AssetTypeId;
            asset.CountryId = dto.CountryId;
            asset.CurrencyId = dto.CurrencyId;
            asset.AssetCategoryId = dto.AssetCategoryId;
            asset.SectorId = dto.SectorId;
            await repository.SaveChangesAsync(cancellationToken);
            return MapToDto(asset);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var asset = await repository.GetByIdAsync(id, cancellationToken);
            if (asset is null)
            {
                return false;
            }

            await repository.DeleteAsync(asset, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task CheckDuplicateAsync(string ticker, int? excludingId, CancellationToken cancellationToken)
        {
            if (await repository.ExistsByTickerAsync(ticker, excludingId, cancellationToken))
            {
                throw new ResourceConflictException("Já existe um ativo com esse ticker.");
            }
        }

        private async Task ValidateReferencesAsync(int typeId, int countryId, int currencyId,
            int categoryId, int sectorId, CancellationToken cancellationToken)
        {
            // Sequential queries: the repositories share the same scoped DbContext.
            if (typeId <= 0 || await assetTypes.GetByIdAsync(typeId, cancellationToken) is null)
            {
                throw new InputValidationException("Selecione um tipo de ativo existente.");
            }
            if (countryId <= 0 || await countries.GetByIdAsync(countryId, cancellationToken) is null)
            {
                throw new InputValidationException("Selecione um país existente.");
            }
            if (currencyId <= 0 || await currencies.GetByIdAsync(currencyId, cancellationToken) is null)
            {
                throw new InputValidationException("Selecione uma moeda existente.");
            }
            if (categoryId <= 0 || await categories.GetByIdAsync(categoryId, cancellationToken) is null)
            {
                throw new InputValidationException("Selecione uma categoria existente.");
            }
            if (sectorId <= 0 || await sectors.GetByIdAsync(sectorId, cancellationToken) is null)
            {
                throw new InputValidationException("Selecione um setor existente.");
            }
        }

        private static AssetDto MapToDto(Asset asset)
        {
            return new AssetDto(asset.Id, asset.Ticker, asset.Name, asset.AssetTypeId,
                asset.CountryId, asset.CurrencyId, asset.AssetCategoryId, asset.SectorId, DateTime.SpecifyKind(asset.CreatedAt, DateTimeKind.Utc));
        }
    }
}
