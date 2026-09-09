using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Common.Validation;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Portfolios.Services
{
    public sealed class PortfolioService(IPortfolioRepository repository, ICurrencyRepository currencies,
        IAssetRepository assets, IExchangeRateService rates, PortfolioDefaults defaults, TimeProvider clock) : IPortfolioService
    {
        private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

        public async Task<IReadOnlyList<PortfolioDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return (await repository.GetAllAsync(cancellationToken)).Select(Map).ToList();
        }

        public async Task<PortfolioDto> CreateAsync(CreatePortfolioDto dto, CancellationToken cancellationToken = default)
        {
            var name = InputRules.RequiredText(dto.Name, "O nome da carteira", 100);
            var description = InputRules.OptionalText(dto.Description, "A descrição", 500);
            var currencyId = dto.BaseCurrencyId
                ?? (await currencies.GetAllAsync(cancellationToken)).FirstOrDefault(x => x.Code == defaults.CurrencyCode)?.Id;
            // The list is untracked; load the chosen currency for the write operation.
            var currency = currencyId.HasValue
                ? await currencies.GetByIdAsync(currencyId.Value, cancellationToken) : null;
            if (currency is null)
                throw new InputValidationException("Cadastre e selecione uma moeda-base válida para a carteira.");
            var portfolio = new Portfolio { Name = name, Description = description,
                BaseCurrencyId = currency.Id, BaseCurrency = currency, CreatedAt = DateTime.UtcNow };
            await repository.AddAsync(portfolio, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return Map(portfolio);
        }

        public async Task<PortfolioDto?> UpdateAsync(int id, UpdatePortfolioDto dto, CancellationToken cancellationToken = default)
        {
            var name = InputRules.RequiredText(dto.Name, "O nome da carteira", 100);
            var description = InputRules.OptionalText(dto.Description, "A descrição", 500);
            var portfolio = await repository.GetByIdAsync(id, cancellationToken);
            if (portfolio is null) return null;
            var currency = await currencies.GetByIdAsync(dto.BaseCurrencyId, cancellationToken)
                ?? throw new InputValidationException("Selecione uma moeda-base existente.");
            portfolio.Name = name;
            portfolio.Description = description;
            portfolio.BaseCurrencyId = currency.Id;
            portfolio.BaseCurrency = currency;
            await repository.SaveChangesAsync(cancellationToken);
            return Map(portfolio);
        }

        public async Task<PortfolioSummaryDto?> GetSummaryAsync(int id, CancellationToken cancellationToken = default)
        {
            var portfolio = await repository.GetByIdAsync(id, cancellationToken);
            if (portfolio is null) return null;
            var positions = await repository.GetPositionsAsync(id, cancellationToken);
            var quotes = new Dictionary<string, ExchangeRateQuote?>();
            foreach (var code in positions.Select(p => p.Asset.Currency.Code).Distinct())
            {
                // Resolve each pair once. No concurrent operations on the scoped DbContext.
                quotes[code] = code == portfolio.BaseCurrency.Code
                    ? new ExchangeRateQuote(code, code, 1m, DateOnly.FromDateTime(DateTime.UtcNow), false, false)
                    : await rates.GetAsync(code, portfolio.BaseCurrency.Code, cancellationToken);
            }
            var result = positions.Select(p =>
            {
                var quote = quotes[p.Asset.Currency.Code];
                return new PositionDto(p.Id, p.AssetId, p.Asset.Ticker, p.Asset.Name, p.Asset.Currency.Code,
                    p.Quantity, p.InvestedAmount, p.CurrentValue,
                    quote is null ? null : p.InvestedAmount * quote.Rate,
                    quote is null ? null : p.CurrentValue * quote.Rate,
                    quote?.Rate, p.Asset.Currency.Code == portfolio.BaseCurrency.Code ? null : quote?.RateDate,
                    quote?.IsStale ?? false, quote?.IsFallback ?? false)
                {
                    UpdatedOn = p.UpdatedOn, Income = p.Income, BaseIncome = quote is null ? null : p.Income * quote.Rate,
                    AssetTypeId = p.Asset.AssetTypeId, AssetTypeName = p.Asset.AssetType?.Name ?? string.Empty,
                    AssetCategoryId = p.Asset.AssetCategoryId, AssetCategoryName = p.Asset.AssetCategory?.Name ?? string.Empty,
                    SectorId = p.Asset.SectorId, SectorName = p.Asset.Sector?.Name ?? string.Empty,
                    CountryId = p.Asset.CountryId, CountryName = p.Asset.Country?.Name ?? string.Empty
                };
            }).ToList();
            var subtotals = positions.GroupBy(p => p.Asset.Currency.Code).OrderBy(g => g.Key)
                .Select(g => new CurrencySubtotalDto(g.Key, g.Sum(p => p.InvestedAmount), g.Sum(p => p.CurrentValue), g.Sum(p => p.Income))).ToList();
            var available = result.All(p => p.ExchangeRate.HasValue);
            return new PortfolioSummaryDto(Map(portfolio), result, subtotals,
                available ? result.Sum(p => p.BaseInvestedAmount!.Value) : null,
                available ? result.Sum(p => p.BaseCurrentValue!.Value) : null,
                available, result.Any(p => p.IsStale), result.Any(p => p.IsFallback),
                available ? result.Sum(p => p.BaseIncome!.Value) : null);
        }

        public async Task<int?> AddPositionAsync(int portfolioId, SavePositionDto dto, CancellationToken cancellationToken = default)
        {
            ValidatePosition(dto);
            if (await repository.GetByIdAsync(portfolioId, cancellationToken) is null) return null;
            await ValidateAssetAsync(portfolioId, dto.AssetId, null, cancellationToken);
            var position = new PortfolioAsset { UpdatedOn = Today, PortfolioId = portfolioId, AssetId = dto.AssetId,
                Quantity = dto.Quantity, InvestedAmount = dto.InvestedAmount, CurrentValue = dto.CurrentValue, Income = dto.Income ?? 0m };
            await repository.AddPositionAsync(position, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return position.Id;
        }

        public async Task<bool> UpdatePositionAsync(int portfolioId, int positionId, SavePositionDto dto,
            CancellationToken cancellationToken = default)
        {
            ValidatePosition(dto);
            var position = await repository.GetPositionAsync(portfolioId, positionId, cancellationToken);
            if (position is null) return false;
            if (position.AssetId != dto.AssetId)
                throw new InputValidationException("O ativo da posição não pode ser trocado. Remova a posição e cadastre outra.");
            position.UpdatedOn = Today;
            position.Quantity = dto.Quantity;
            position.InvestedAmount = dto.InvestedAmount;
            position.CurrentValue = dto.CurrentValue;
            if (dto.Income.HasValue) position.Income = dto.Income.Value;
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeletePositionAsync(int portfolioId, int positionId, CancellationToken cancellationToken = default)
        {
            var position = await repository.GetPositionAsync(portfolioId, positionId, cancellationToken);
            if (position is null) return false;
            repository.RemovePosition(position);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task ValidateAssetAsync(int portfolioId, int assetId, int? excludingId, CancellationToken cancellationToken)
        {
            if (assetId <= 0 || await assets.GetByIdAsync(assetId, cancellationToken) is null)
                throw new InputValidationException("Selecione um ativo existente.");
            if (await repository.HasAssetAsync(portfolioId, assetId, excludingId, cancellationToken))
                throw new ResourceConflictException("O ativo já possui uma posição nesta carteira.");
        }

        private static void ValidatePosition(SavePositionDto dto)
        {
            if (dto.Quantity < 0 || dto.Quantity > 9999999999999.999999m || decimal.Round(dto.Quantity, 6) != dto.Quantity)
                throw new InputValidationException("Quantidade deve ser não negativa, com até 13 inteiros e 6 casas decimais.");
            foreach (var value in new[] { dto.InvestedAmount, dto.CurrentValue, dto.Income ?? 0m })
            {
                if (value < 0 || value > 999999999999999.9999m || decimal.Round(value, 4) != value)
                    throw new InputValidationException("Valores devem ser não negativos, com até 15 inteiros e 4 casas decimais.");
            }
        }

        private static PortfolioDto Map(Portfolio p) => new(p.Id, p.Name, p.Description, p.BaseCurrencyId, p.BaseCurrency.Code);
    }
}
