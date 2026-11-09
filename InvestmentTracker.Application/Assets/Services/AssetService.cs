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
        ISectorRepository sectors, IAssetImageRepository images) : IAssetService
    {
        public Task<AssetImageFile?> GetImageAsync(int id, CancellationToken cancellationToken = default) => images.ReadAsync(id, cancellationToken);
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
            var imageVersion = dto.Image is null ? null : AssetImageRules.Validate(dto.Image, null);
            var currency = await ValidateReferencesAsync(dto.AssetTypeId, dto.CountryId, dto.CurrencyId,
                dto.AssetCategoryId, dto.SectorId, cancellationToken);
            ValidateListingCurrency(dto.Listing?.Market, currency);
            await CheckDuplicateAsync(ticker, null, cancellationToken);

            var asset = new Asset
            {
                Ticker = ticker, Name = name, AssetTypeId = dto.AssetTypeId,
                CountryId = dto.CountryId, CurrencyId = dto.CurrencyId,
                AssetCategoryId = dto.AssetCategoryId, SectorId = dto.SectorId,
                CreatedAt = DateTime.UtcNow, ImageVersion = imageVersion
            };
            await repository.AddAsync(asset, cancellationToken);
            ApplyListing(asset, dto.Listing);
            if (dto.Image?.Data is not null) await images.StageAsync(asset, dto.Image.Data, cancellationToken);
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

            var currency = await ValidateReferencesAsync(dto.AssetTypeId, dto.CountryId, dto.CurrencyId,
                dto.AssetCategoryId, dto.SectorId, cancellationToken);
            ValidateListingCurrency(dto.Listing?.Market ?? asset.QuoteMarket, currency);
            await CheckDuplicateAsync(ticker, id, cancellationToken);

            if (asset.CurrencyId != dto.CurrencyId && await repository.HasPositionsAsync(id, cancellationToken))
                throw new ResourceConflictException("A moeda de um ativo com posições não pode ser alterada.");
            if (dto.Image is not null)
            {
                asset.ImageVersion = AssetImageRules.Validate(dto.Image, asset.ImageVersion);
                await images.StageAsync(asset, dto.Image.Data, cancellationToken);
            }
            ApplyListing(asset, dto.Listing);

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

        private async Task<Currency> ValidateReferencesAsync(int typeId, int countryId, int currencyId,
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
            var currency = currencyId <= 0 ? null : await currencies.GetByIdAsync(currencyId, cancellationToken);
            if (currency is null)
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
            return currency;
        }

        private static AssetDto MapToDto(Asset asset)
        {
            return new AssetDto(asset.Id, asset.Ticker, asset.Name, asset.AssetTypeId,
                asset.CountryId, asset.CurrencyId, asset.AssetCategoryId, asset.SectorId, DateTime.SpecifyKind(asset.CreatedAt, DateTimeKind.Utc)) { ImageVersion = asset.ImageVersion,
                Listing = asset.QuoteMarket is null ? null : new(asset.QuoteMarket, asset.QuoteSymbol!) };
        }
        private static void ApplyListing(Asset asset, AssetListingChange? listing)
        {
            if (listing is null) return;
            if (listing.Market == "" && listing.Symbol == "") { asset.QuoteMarket = null; asset.QuoteSymbol = null; return; }
            AssetDiscoveryService.Validate(listing.Market, listing.Symbol);
            asset.QuoteMarket = listing.Market;
            asset.QuoteSymbol = listing.Symbol.Trim().ToUpperInvariant();
        }
        private static void ValidateListingCurrency(string? market, Currency currency)
        {
            if (market is "BR" && currency.Code != "BRL" || market is "US" && currency.Code != "USD")
                throw new InputValidationException("O mercado de consulta exige BRL no Brasil ou USD nos EUA. Confira a moeda do ativo.");
        }
    }
}
