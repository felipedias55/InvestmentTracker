using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Income
{
    public sealed class IncomeService(IIncomeRepository repository, IAssetRepository assets,
        ICurrencyRepository currencies, IExternalAssetRepository cash, TimeProvider clock, IIncomeConversionService conversions) : IIncomeService
    {
        private const decimal MaxMoney = 999999999999999.9999m;
        private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

        public async Task<IReadOnlyList<IncomeDto>> ListAsync(int portfolioId, CancellationToken ct)
            => (await repository.ListAsync(portfolioId, ct)).Select(Map).ToList();

        public async Task<IncomeDto?> SaveAsync(int portfolioId, SaveIncomeDto dto, CancellationToken ct)
        {
            if (dto.RequestId == Guid.Empty || dto.AssetId <= 0)
                throw new InputValidationException("Informe um recebimento e um ativo válidos.");
            if (dto.Date < new DateOnly(1900, 1, 1) || dto.Date > Today)
                throw new InputValidationException("Informe uma data válida, até hoje.");
            if (dto.Amount <= 0 || dto.Amount > MaxMoney || decimal.Round(dto.Amount, 4) != dto.Amount)
                throw new InputValidationException("Informe o valor líquido positivo, com até 15 inteiros e 4 casas decimais.");
            IncomeConversionService.ValidateAmount(dto.BaseAmount);
            var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            if (notes?.Length > 500) throw new InputValidationException("A observação deve ter até 500 caracteres.");
            var receipt = await repository.ExecuteAsync(portfolioId, dto.RequestId, async (portfolio, existing, token) =>
            {
                if (existing is not null)
                {
                    if (existing.Date != dto.Date || existing.AssetId != dto.AssetId || existing.Amount != dto.Amount ||
                        existing.CashAssetId != dto.CashAssetId || existing.Notes != notes || existing.RequestedBaseAmount != dto.BaseAmount)
                        throw new ResourceConflictException("Esta solicitação já foi usada com outros dados. Atualize a página.");
                    return existing;
                }
                var position = await repository.PositionAsync(portfolioId, dto.AssetId, token)
                    ?? throw new InputValidationException("Selecione um ativo com posição nesta carteira, mesmo que esteja zerada.");
                var asset = await assets.GetByIdAsync(dto.AssetId, token)
                    ?? throw new InputValidationException("Ativo indisponível.");
                var currency = await currencies.GetByIdAsync(asset.CurrencyId, token)
                    ?? throw new InputValidationException("Moeda indisponível.");
                if (position.Income + dto.Amount > MaxMoney)
                    throw new InputValidationException("O total de proventos excede o limite permitido.");
                var result = new IncomeReceipt { PortfolioId = portfolioId, PositionId = position.Id,
                    AssetId = dto.AssetId, RequestId = dto.RequestId, Date = dto.Date, Amount = dto.Amount,
                    Ticker = asset.Ticker, CurrencyCode = currency.Code, CashAssetId = dto.CashAssetId,
                    BaseCurrencyCode = portfolio.BaseCurrency.Code, RequestedBaseAmount = dto.BaseAmount,
                    Notes = notes, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
                var conversion = await conversions.BuildAsync(result, portfolio.BaseCurrency.Code, dto.BaseAmount,
                    dto.RequestId, 1, "Conversão no registro do recebimento", token);
                if (conversion is not null) result.Conversions.Add(conversion);
                if (dto.CashAssetId.HasValue)
                {
                    var balance = await cash.GetByIdAsync(portfolioId, dto.CashAssetId.Value, token);
                    if (balance is null || balance.CurrencyId != asset.CurrencyId)
                        throw new InputValidationException("Selecione um saldo desta carteira na mesma moeda do ativo.");
                    if (balance.Value + dto.Amount > MaxMoney)
                        throw new InputValidationException("O saldo excede o limite permitido.");
                    balance.Value += dto.Amount;
                    balance.UpdatedOn = Today;
                    result.CashAssetName = balance.Name;
                }
                position.Income += dto.Amount;
                position.UpdatedOn = Today;
                return result;
            }, ct);
            return receipt is null ? null : Map(receipt);
        }
        public static IncomeDto Map(IncomeReceipt r) => new(r.Id, r.Date, r.Ticker, r.CurrencyCode, r.Amount, r.CashAssetName, r.Notes)
            { BaseCurrencyCode = r.BaseCurrencyCode, Conversions = r.Conversions.OrderByDescending(c => c.Revision).ThenBy(c => c.BaseCurrencyCode).Select(IncomeConversionService.Map).ToList() };
    }
}
