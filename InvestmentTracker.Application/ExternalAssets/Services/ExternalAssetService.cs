using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Common.Validation;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.ExternalAssets.Dtos;
using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.ExternalAssets.Services
{
    public sealed class ExternalAssetService(IExternalAssetRepository repository, IPortfolioRepository portfolios,
        ICurrencyRepository currencies, IExchangeRateService rates, TimeProvider clock) : IExternalAssetService
    {
        public async Task<ExternalAssetSummaryDto?> GetSummaryAsync(int portfolioId, CancellationToken cancellationToken = default)
        {
            var portfolio = await portfolios.GetByIdAsync(portfolioId, cancellationToken);
            if (portfolio is null) return null;
            var items = await repository.GetAllAsync(portfolioId, cancellationToken);
            var quotes = new Dictionary<string, ExchangeRateQuote?>();
            foreach (var code in items.Select(a => a.Currency.Code).Distinct())
                quotes[code] = code == portfolio.BaseCurrency.Code
                    ? new ExchangeRateQuote(code, code, 1m, DateOnly.FromDateTime(DateTime.UtcNow), false, false)
                    : await rates.GetAsync(code, portfolio.BaseCurrency.Code, cancellationToken);
            var result = items.Select(a =>
            {
                var quote = quotes[a.Currency.Code];
                return new ExternalAssetDto(a.Id, a.Name, a.CurrencyId, a.Currency.Code, a.Value, a.Description,
                    quote is null ? null : a.Value * quote.Rate, quote?.Rate,
                    a.Currency.Code == portfolio.BaseCurrency.Code ? null : quote?.RateDate,
                    quote?.IsStale ?? false, quote?.IsFallback ?? false) { UpdatedOn = a.UpdatedOn };
            }).ToList();
            var available = result.All(a => a.BaseValue.HasValue);
            return new ExternalAssetSummaryDto(new(portfolio.Id, portfolio.Name, portfolio.Description,
                portfolio.BaseCurrencyId, portfolio.BaseCurrency.Code), result,
                available ? result.Sum(a => a.BaseValue!.Value) : null, available,
                result.Any(a => a.IsStale), result.Any(a => a.IsFallback));
        }

        public async Task<int?> CreateAsync(int portfolioId, SaveExternalAssetDto dto, CancellationToken cancellationToken = default)
        {
            if (await portfolios.GetByIdAsync(portfolioId, cancellationToken) is null) return null;
            var asset = new ExternalAsset { PortfolioId = portfolioId };
            await ApplyAsync(asset, dto, cancellationToken);
            await repository.AddAsync(asset, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return asset.Id;
        }

        public async Task<bool> UpdateAsync(int portfolioId, int id, SaveExternalAssetDto dto, CancellationToken cancellationToken = default)
        {
            var asset = await repository.GetByIdAsync(portfolioId, id, cancellationToken);
            if (asset is null) return false;
            await ApplyAsync(asset, dto, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(int portfolioId, int id, CancellationToken cancellationToken = default)
        {
            var asset = await repository.GetByIdAsync(portfolioId, id, cancellationToken);
            if (asset is null) return false;
            repository.Remove(asset);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task ApplyAsync(ExternalAsset asset, SaveExternalAssetDto dto, CancellationToken cancellationToken)
        {
            var name = InputRules.RequiredText(dto.Name, "O nome", 200);
            var description = InputRules.OptionalText(dto.Description, "A descrição", 500);
            if (dto.Value < 0m || dto.Value > 999999999999999.9999m || decimal.Round(dto.Value, 4) != dto.Value)
                throw new InputValidationException("O valor deve ser não negativo, com até 15 inteiros e quatro casas decimais.");
            var currency = await currencies.GetByIdAsync(dto.CurrencyId, cancellationToken)
                ?? throw new InputValidationException("Selecione uma moeda existente.");
            asset.UpdatedOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
                TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
            asset.Name = name; asset.Description = description; asset.Value = dto.Value;
            asset.CurrencyId = currency.Id; asset.Currency = currency;
        }
    }
}
