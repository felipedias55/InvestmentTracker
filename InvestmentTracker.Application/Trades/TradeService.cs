using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Trades
{
    public sealed class TradeService(ITradeRepository repository, IAssetRepository assets,
        IExternalAssetRepository cash, ICurrencyRepository currencies, IExchangeRateService rates, TimeProvider clock) : ITradeService
    {
        private const decimal MaxMoney = 999999999999999.9999m;
        private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        public async Task<IReadOnlyList<TradeDto>> ListAsync(int portfolioId, CancellationToken ct)
            => (await repository.ListAsync(portfolioId, ct)).Select(Map).ToList();
        public async Task<TradeDto?> SaveAsync(int portfolioId, SaveTradeDto dto, CancellationToken ct)
        {
            if (dto.RequestId == Guid.Empty || dto.Kind is not ("buy" or "sell"))
                throw new InputValidationException("Informe uma operação de compra ou venda válida.");
            if (dto.Date < new DateOnly(1900, 1, 1) || dto.Date > Today)
                throw new InputValidationException("Informe uma data válida, até hoje.");
            if (dto.Quantity <= 0m || dto.Quantity > 9999999999999.999999m || decimal.Round(dto.Quantity, 6) != dto.Quantity)
                throw new InputValidationException("Quantidade deve ser positiva e ter até 6 casas decimais.");
            Money(dto.UnitPrice);
            if (dto.BaseAmount.HasValue) Money(dto.BaseAmount.Value);
            decimal amount;
            try { amount = decimal.Round(dto.Quantity * dto.UnitPrice, 4, MidpointRounding.AwayFromZero); }
            catch (OverflowException) { throw new InputValidationException("O total da operação excede o limite permitido."); }
            Money(amount);
            var saved = await repository.ExecuteAsync(portfolioId, dto.RequestId, async (portfolio, existing, token) =>
            {
                if (existing is not null)
                {
                    if (existing.AssetId != dto.AssetId || existing.Date != dto.Date || existing.Kind != dto.Kind ||
                        existing.Quantity != dto.Quantity || existing.UnitPrice != dto.UnitPrice || existing.CashAssetId != dto.CashAssetId ||
                        existing.RequestedBaseAmount != dto.BaseAmount)
                        throw new ResourceConflictException("Esta solicitação já foi utilizada com outros dados. Atualize a tela.");
                    return existing;
                }
                var latest = await repository.LatestDateAsync(portfolioId, token);
                if (latest.HasValue && dto.Date < latest)
                    throw new InputValidationException("A operação não pode ser anterior à última operação ou fotografia. Os saldos anteriores já estão consolidados.");
                var asset = await assets.GetByIdAsync(dto.AssetId, token)
                    ?? throw new InputValidationException("Selecione um ativo existente.");
                var currency = await currencies.GetByIdAsync(asset.CurrencyId, token)
                    ?? throw new InputValidationException("Moeda do ativo indisponível.");
                var position = await repository.PositionAsync(portfolioId, asset.Id, token);
                if (dto.Kind == "sell" && (position is null || dto.Quantity > position.Quantity))
                    throw new InputValidationException("A quantidade vendida excede a posição disponível.");
                if (position is null)
                {
                    position = new PortfolioAsset { PortfolioId = portfolioId, AssetId = asset.Id };
                    repository.AddPosition(position);
                }
                var quantity = dto.Kind == "buy" ? position.Quantity + dto.Quantity : position.Quantity - dto.Quantity;
                if (quantity > 9999999999999.999999m) throw new InputValidationException("Quantidade acumulada excede o limite.");
                var cost = dto.Kind == "buy" ? position.InvestedAmount + amount : quantity == 0m ? 0m :
                    decimal.Round(position.InvestedAmount * (quantity / position.Quantity), 4, MidpointRounding.AwayFromZero);
                decimal value;
                try { value = decimal.Round(quantity * dto.UnitPrice, 4, MidpointRounding.AwayFromZero); }
                catch (OverflowException) { throw new InputValidationException("O valor da posição excede o limite permitido."); }
                if (cost > MaxMoney || value > MaxMoney) throw new InputValidationException("O saldo da posição excede o limite permitido.");
                position.Quantity = quantity; position.InvestedAmount = cost; position.CurrentValue = value; position.UpdatedOn = Today;
                var trade = new PortfolioTrade { PortfolioId = portfolioId, AssetId = asset.Id, RequestId = dto.RequestId,
                    Date = dto.Date, Kind = dto.Kind, Ticker = asset.Ticker, CurrencyCode = currency.Code,
                    Quantity = dto.Quantity, UnitPrice = dto.UnitPrice, Amount = amount, RemainingQuantity = quantity,
                    RemainingCost = cost, CashAssetId = dto.CashAssetId, RequestedBaseAmount = dto.BaseAmount,
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
                if (dto.CashAssetId.HasValue)
                {
                    var balance = await cash.GetByIdAsync(portfolioId, dto.CashAssetId.Value, token);
                    if (balance is null || balance.CurrencyId != asset.CurrencyId)
                        throw new InputValidationException("Selecione um saldo desta carteira na mesma moeda do ativo.");
                    var newBalance = balance.Value + (dto.Kind == "buy" ? -amount : amount);
                    if (newBalance < 0m || newBalance > MaxMoney)
                        throw new InputValidationException("O saldo selecionado é insuficiente ou excede o limite permitido.");
                    balance.Value = newBalance; balance.UpdatedOn = Today; trade.CashAssetName = balance.Name;
                }
                else
                {
                    decimal? baseAmount = currency.Code == portfolio.BaseCurrency.Code ? amount : dto.BaseAmount;
                    if (currency.Code == portfolio.BaseCurrency.Code && dto.BaseAmount.HasValue && dto.BaseAmount != amount)
                        throw new InputValidationException("Na mesma moeda o equivalente deve ser igual ao total da operação.");
                    if (!baseAmount.HasValue && dto.Date == Today)
                    {
                        var quote = await rates.GetAsync(currency.Code, portfolio.BaseCurrency.Code, token);
                        if (quote is not null)
                        {
                            try { baseAmount = decimal.Round(amount * quote.Rate, 4, MidpointRounding.AwayFromZero); }
                            catch (OverflowException) { throw new InputValidationException("O equivalente excede o limite permitido."); }
                        }
                    }
                    if (!baseAmount.HasValue)
                        throw new InputValidationException("Informe o equivalente na moeda-base: não há câmbio automático disponível para esta operação.");
                    Money(baseAmount.Value);
                    repository.AddFlow(new PortfolioCashFlow { PortfolioId = portfolioId, Date = dto.Date,
                        Kind = dto.Kind == "buy" ? "contribution" : "withdrawal", CurrencyId = asset.CurrencyId,
                        Amount = amount, BaseCurrencyCode = portfolio.BaseCurrency.Code, BaseAmount = baseAmount,
                        Notes = $"{(dto.Kind == "buy" ? "Compra" : "Venda")} de {asset.Ticker} — registro automático",
                        CreatedAtUtc = clock.GetUtcNow().UtcDateTime, UpdatedAtUtc = clock.GetUtcNow().UtcDateTime }, trade);
                }
                return trade;
            }, ct);
            return saved is null ? null : Map(saved);
        }
        private static void Money(decimal value)
        {
            if (value <= 0 || value > MaxMoney || decimal.Round(value, 4) != value)
                throw new InputValidationException("Informe um valor positivo com até 15 inteiros e 4 casas decimais.");
        }
        private static TradeDto Map(PortfolioTrade t) => new(t.Id, t.Date, t.Kind, t.Ticker, t.CurrencyCode,
            t.Quantity, t.UnitPrice, t.Amount, t.CashAssetName);
    }
}
