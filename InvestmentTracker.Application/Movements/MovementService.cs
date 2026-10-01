using System.Text.Json;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Common.Validation;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Movements
{
    public sealed class MovementService(IMovementRepository repository, IExternalAssetRepository cash,
        ICurrencyRepository currencies, IExchangeRateService rates, TimeProvider clock,
        InvestmentTracker.Application.Assets.Interfaces.IAssetRepository assets) : IMovementService
    {
        private const decimal MaxMoney = 999999999999999.9999m;
        private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        public async Task<IReadOnlyList<MovementDto>> ListAsync(int portfolioId, CancellationToken ct)
        {
            var all = await repository.ListAsync(portfolioId, ct);
            var result = new List<MovementDto>();
            foreach (var m in all)
                result.Add(await MapAsync(m, all.FirstOrDefault(x => x.ReversalOfId == m.Id)?.Id, ct));
            return result;
        }
        public async Task<MovementDto?> SaveAsync(int portfolioId, SaveMovementDto dto, CancellationToken ct)
        {
            if (dto.RequestId == Guid.Empty || dto.Kind is not ("deposit" or "withdrawal" or "transfer" or "adjustment" or "reopen"))
                throw new InputValidationException("Informe uma movimentação válida.");
            Money(dto.Amount, dto.Kind is "adjustment" or "reopen");
            if (dto.Kind == "reopen" && (dto.Amount != 0 || dto.CashAssetId != 0 || dto.BaseAmount.HasValue))
                throw new InputValidationException("Reabertura não movimenta dinheiro.");
            if (dto.Date < new DateOnly(1900, 1, 1) || dto.Date > Today) throw new InputValidationException("Informe uma data válida, até hoje.");
            if (dto.Kind != "transfer" && dto.DestinationId.HasValue) throw new InputValidationException("Destino adicional é permitido somente em transferências.");
            if (dto.Kind is "transfer" or "adjustment" && dto.BaseAmount.HasValue) throw new InputValidationException("Este movimento não recebe equivalente de aporte.");
            var reason = dto.Kind is "adjustment" or "reopen" ? InputRules.RequiredText(dto.Reason, "O motivo", 400)
                : InputRules.OptionalText(dto.Reason, "A observação", 400);
            var payload = JsonSerializer.Serialize(dto with { Reason = reason });
            var result = await repository.ExecuteAsync(portfolioId, dto.RequestId, async (portfolio, existing, token) =>
            {
                if (existing is not null) { SameRequest(existing, payload); return existing; }
                if (dto.Kind == "reopen")
                {
                    if (await repository.ReopenAsync(portfolioId, dto.Date, token) == 0)
                        throw new InputValidationException("Não há fotografias a reabrir a partir desta data.");
                    return new FinancialMovement { PortfolioId = portfolioId, RequestId = dto.RequestId, Date = dto.Date,
                        Kind = "reopen", Description = reason!, CurrencyCode = portfolio.BaseCurrency.Code,
                        CreatedAtUtc = clock.GetUtcNow().UtcDateTime, RequestPayload = payload };
                }
                var source = await cash.GetByIdAsync(portfolioId, dto.CashAssetId, token)
                    ?? throw new InputValidationException("Selecione um saldo desta carteira.");
                var currency = await currencies.GetByIdAsync(source.CurrencyId, token)
                    ?? throw new InputValidationException("Moeda indisponível.");
                var after = dto.Kind switch { "deposit" => source.Value + dto.Amount, "adjustment" => dto.Amount, _ => source.Value - dto.Amount };
                Money(after, true);
                if (dto.Kind == "adjustment" && after == source.Value) throw new InputValidationException("O novo saldo é igual ao atual.");
                var movement = new FinancialMovement { PortfolioId = portfolioId, RequestId = dto.RequestId, Date = dto.Date,
                    Kind = dto.Kind, Amount = dto.Kind == "adjustment" ? Math.Abs(after - source.Value) : dto.Amount,
                    CurrencyCode = currency.Code, Description = reason ?? source.Name, CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
                    RequestPayload = payload };
                if (dto.Kind == "transfer")
                {
                    var destination = dto.DestinationId.HasValue ? await cash.GetByIdAsync(portfolioId, dto.DestinationId.Value, token) : null;
                    if (destination is null || destination.Id == source.Id || destination.CurrencyId != source.CurrencyId)
                        throw new InputValidationException("Escolha outro saldo desta carteira na mesma moeda. Transferências com câmbio não são suportadas neste fluxo.");
                    Money(destination.Value + dto.Amount, true);
                    destination.Value += dto.Amount; destination.UpdatedOn = Today;
                }
                if (dto.Kind is "deposit" or "withdrawal")
                {
                    var equivalent = currency.Code == portfolio.BaseCurrency.Code ? dto.Amount : dto.BaseAmount;
                    if (currency.Code == portfolio.BaseCurrency.Code && dto.BaseAmount.HasValue && dto.BaseAmount != dto.Amount)
                        throw new InputValidationException("O equivalente deve ser igual ao valor na mesma moeda.");
                    if (!equivalent.HasValue && dto.Date == Today)
                    {
                        var quote = await rates.GetAsync(currency.Code, portfolio.BaseCurrency.Code, token);
                        try { if (quote is not null) equivalent = decimal.Round(dto.Amount * quote.Rate, 4, MidpointRounding.AwayFromZero); }
                        catch (OverflowException) { throw new InputValidationException("O equivalente excede o limite."); }
                    }
                    if (!equivalent.HasValue) throw new InputValidationException("Informe o equivalente na moeda-base na data da movimentação.");
                    Money(equivalent.Value);
                    repository.AddFlow(new PortfolioCashFlow { PortfolioId = portfolioId, Date = dto.Date,
                        Kind = dto.Kind == "deposit" ? "contribution" : "withdrawal", CurrencyId = currency.Id, Amount = dto.Amount,
                        BaseAmount = equivalent, BaseCurrencyCode = portfolio.BaseCurrency.Code,
                        Notes = $"Movimentação: {movement.Description}", CreatedAtUtc = movement.CreatedAtUtc, UpdatedAtUtc = movement.CreatedAtUtc }, movement);
                }
                source.Value = after; source.UpdatedOn = Today;
                return movement;
            }, ct);
            return result is null ? null : await MapAsync(result, null, ct);
        }
        public async Task<MovementDto?> CorporateEventAsync(int portfolioId, SaveCorporateEventDto dto, CancellationToken ct)
        {
            if (dto.RequestId == Guid.Empty || dto.Kind is not ("split" or "reverse-split" or "bonus") || dto.Date < new DateOnly(1900, 1, 1) || dto.Date > Today)
                throw new InputValidationException("Informe evento e data válidos.");
            if (dto.Quantity <= 0 || dto.Quantity > 9999999999999.999999m || decimal.Round(dto.Quantity, 6) != dto.Quantity)
                throw new InputValidationException("Quantidade deve ser positiva, com até 6 casas decimais.");
            Money(dto.Cost, true);
            if (dto.Kind != "bonus" && dto.Cost != 0) throw new InputValidationException("Desdobramento e grupamento preservam o custo total.");
            var reason = InputRules.RequiredText(dto.Reason, "O comunicado ou motivo", 400);
            var payload = JsonSerializer.Serialize(dto with { Reason = reason });
            var saved = await repository.ExecuteAsync(portfolioId, dto.RequestId, async (_, existing, token) =>
            {
                if (existing is not null) { SameRequest(existing, payload); return existing; }
                var position = await repository.PositionAsync(portfolioId, dto.PositionId, token)
                    ?? throw new InputValidationException("Selecione uma posição desta carteira.");
                if (position.Quantity <= 0) throw new InputValidationException("O evento exige uma posição com quantidade positiva.");
                var quantity = dto.Kind == "bonus" ? position.Quantity + dto.Quantity : dto.Quantity;
                if (quantity > 9999999999999.999999m || (dto.Kind == "split" && quantity <= position.Quantity) || (dto.Kind == "reverse-split" && quantity >= position.Quantity))
                    throw new InputValidationException("A quantidade final deve aumentar no desdobramento e diminuir no grupamento, dentro do limite permitido.");
                Money(position.InvestedAmount + dto.Cost, true);
                var asset = (await assets.GetByIdAsync(position.AssetId, token))!;
                var currency = (await currencies.GetByIdAsync(asset.CurrencyId, token))!;
                position.Quantity = quantity; position.InvestedAmount += dto.Cost; position.UpdatedOn = Today;
                // Preserve market value until a real ex-event quote is supplied. No cash or income is invented.
                return new FinancialMovement { PortfolioId = portfolioId, RequestId = dto.RequestId, Date = dto.Date,
                    Kind = dto.Kind, Amount = dto.Cost, CurrencyCode = currency.Code, Description = $"{asset.Ticker}: {reason}",
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime, RequestPayload = payload };
            }, ct);
            return saved is null ? null : await MapAsync(saved, null, ct);
        }
        public async Task<MovementDto?> ReverseAsync(int portfolioId, int id, ReverseMovementDto dto, CancellationToken ct)
        {
            if (dto.RequestId == Guid.Empty) throw new InputValidationException("Identificador inválido.");
            var reason = InputRules.RequiredText(dto.Reason, "O motivo do estorno", 400);
            var payload = JsonSerializer.Serialize(new { ReversalOfId = id, Reason = reason });
            var result = await repository.ExecuteAsync(portfolioId, dto.RequestId, async (_, existing, token) =>
            {
                if (existing is not null) { SameRequest(existing, payload); return existing; }
                var original = await repository.GetAsync(portfolioId, id, token)
                    ?? throw new InputValidationException("Movimentação não encontrada nesta carteira.");
                var blocked = await BlockedAsync(original, await repository.IsReversedAsync(id, token), token);
                if (blocked is not null) throw new ResourceConflictException(blocked);
                var movement = new FinancialMovement { PortfolioId = portfolioId, RequestId = dto.RequestId,
                    Date = Today, Kind = "reversal", ReversalOfId = id, Amount = original.Amount, CurrencyCode = original.CurrencyCode,
                    Description = reason, CreatedAtUtc = clock.GetUtcNow().UtcDateTime, RequestPayload = payload };
                foreach (var effect in original.Effects)
                {
                    if (effect.PositionId.HasValue)
                    {
                        var p = (await repository.PositionAsync(portfolioId, effect.PositionId.Value, token))!;
                        p.Quantity = effect.BeforeQuantity; p.InvestedAmount = effect.BeforeCost;
                        p.CurrentValue = effect.BeforeValue; p.Income = effect.BeforeIncome; p.UpdatedOn = Today;
                    }
                    else
                    {
                        var balance = (await cash.GetByIdAsync(portfolioId, effect.CashAssetId!.Value, token))!;
                        balance.Value = effect.BeforeValue; balance.UpdatedOn = Today;
                    }
                }
                foreach (var flow in await repository.FlowsAsync(id, token))
                    repository.AddFlow(new PortfolioCashFlow { PortfolioId = portfolioId, Date = Today,
                        Kind = flow.Kind, IsReversal = true, CurrencyId = flow.CurrencyId,
                        Amount = flow.Amount, BaseCurrencyCode = flow.BaseCurrencyCode, BaseAmount = flow.BaseAmount,
                        Notes = $"Estorno da movimentação #{id}: {reason}", CreatedAtUtc = movement.CreatedAtUtc,
                        UpdatedAtUtc = movement.CreatedAtUtc }, movement);
                return movement;
            }, ct);
            return result is null ? null : await MapAsync(result, null, ct);
        }
        private async Task<string?> BlockedAsync(FinancialMovement m, bool reversed, CancellationToken ct)
        {
            if (reversed) return "Movimentação já estornada.";
            if (m.ReversalOfId.HasValue) return "Um estorno não pode ser estornado. Registre a operação correta novamente.";
            if (m.Kind == "income-conversion") return "Conversão auditável sem efeito de saldo. Para corrigir, registre uma nova versão em Proventos.";
            if (m.Kind == "reopen") return "Reabertura registrada na auditoria. Uma nova fotografia do mês atual encerra a reabertura desse mês.";
            if (m.Kind == "opening") return "Saldo inicial: use um ajuste justificado.";
            if (m.Effects.Count == 0 && m.Kind != "historical") return "Registro anterior à auditoria de saldos; não há valores anteriores suficientes para estorno automático.";
            if (await repository.HasDependentAsync(m, ct)) return "Existem movimentações posteriores dependentes. Estorne-as primeiro.";
            foreach (var e in m.Effects)
            {
                if (e.PositionId.HasValue)
                {
                    var p = await repository.PositionAsync(m.PortfolioId, e.PositionId.Value, ct);
                    if (p is null || (await assets.GetByIdAsync(p.AssetId, ct))?.CurrencyId != e.CurrencyId || p.Quantity != e.AfterQuantity || p.InvestedAmount != e.AfterCost || p.CurrentValue != e.AfterValue || p.Income != e.AfterIncome)
                        return "A posição mudou após o lançamento. Confira os ajustes antes de estornar.";
                }
                else
                {
                    var b = await cash.GetByIdAsync(m.PortfolioId, e.CashAssetId!.Value, ct);
                    if (b is null || b.CurrencyId != e.CurrencyId || b.Value != e.AfterValue)
                        return "O saldo mudou após o lançamento. Confira os ajustes antes de estornar.";
                }
            }
            return null;
        }
        private async Task<MovementDto> MapAsync(FinancialMovement m, int? reversedBy, CancellationToken ct)
        {
            var blocked = await BlockedAsync(m, reversedBy.HasValue, ct);
            var effects = new List<MovementEffectDto>();
            foreach (var e in m.Effects)
                effects.Add(new(e.Name, (await currencies.GetByIdAsync(e.CurrencyId, ct))?.Code ?? "", e.BeforeValue, e.AfterValue,
                    e.PositionId.HasValue, e.BeforeQuantity, e.AfterQuantity, e.BeforeCost, e.AfterCost, e.BeforeIncome, e.AfterIncome));
            return new(m.Id, m.Date, m.Kind, m.Description, m.CurrencyCode, m.Amount, m.TradeId, m.IncomeReceiptId,
                m.ReversalOfId, reversedBy, blocked is null, blocked, m.CreatedAtUtc, effects);
        }
        private static void SameRequest(FinancialMovement m, string payload)
        { if (m.RequestPayload != payload) throw new ResourceConflictException("Solicitação já utilizada com outros dados. Atualize a página."); }
        private static void Money(decimal value, bool zero = false)
        {
            if (value < 0 || (!zero && value == 0) || value > MaxMoney || decimal.Round(value, 4) != value)
                throw new InputValidationException("Saldo insuficiente ou valor inválido. Use até 15 inteiros e 4 decimais.");
        }
    }
}
